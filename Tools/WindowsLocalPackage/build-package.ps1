[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$PublicMasterSha,
    [Parameter(Mandatory = $true)][string]$GameDataRoot,
    [Parameter(Mandatory = $true)][string]$RuntimeArchive,
    [Parameter(Mandatory = $true)][string]$MySqlArchive,
    [Parameter(Mandatory = $true)][string]$DependencyMetadata,
    [Parameter(Mandatory = $true)][string]$ClientManifest,
    [string]$AspNetRuntimeArchive,
    [Parameter(Mandatory = $true)][string]$VcRedistributable,
    [switch]$SkipCoreBuild
)

# A producer may invoke this from a PowerShell 7 parent process. Its inherited
# module path must not hide the standard modules belonging to this runtime.
$env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules') + [IO.Path]::PathSeparator + $env:PSModulePath
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop
Import-Module Microsoft.PowerShell.Security -ErrorAction Stop
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Executable"
    }
}

function Read-Git {
    param([string[]]$Arguments)
    $result = & git -C $repositoryRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git operation failed: $($Arguments[0])"
    }
    return $result
}

function Require-File {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file is missing: $Path"
    }
    return [IO.Path]::GetFullPath($Path)
}

function Get-SafeFiles {
    param([string]$Root)
    $directory = Get-Item -LiteralPath $Root
    if (-not $directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "A regular directory is required: $Root"
    }
    $entries = @(Get-ChildItem -LiteralPath $Root -Recurse -Force)
    foreach ($entry in $entries) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Reparse points cannot be packaged: $($entry.FullName)"
        }
    }
    return @($entries | Where-Object { -not $_.PSIsContainer })
}

function Copy-Tree {
    param([string]$Source, [string]$Destination)
    $files = @(Get-SafeFiles $Source)
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $prefix = [IO.Path]::GetFullPath($Source).TrimEnd('\') + '\'
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($prefix.Length)
        $target = Join-Path $Destination $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}

function Remove-StagingDirectory {
    param([string]$Path)
    $resolved = [IO.Path]::GetFullPath($Path)
    $prefix = $script:workRoot.TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing deletion outside this package staging directory: $resolved"
    }
    if (Test-Path -LiteralPath $resolved) {
        Get-SafeFiles $resolved | Out-Null
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

function Get-Property {
    param($Object, [string]$Name, [switch]$Optional)
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        if ($Optional) { return $null }
        throw "Dependency metadata is missing '$Name'."
    }
    return $property.Value
}

function Verify-Dependency {
    param([string]$Archive, $Metadata, [string]$Name, [string[]]$AllowedHosts)
    $path = Require-File $Archive
    $version = [string](Get-Property $Metadata 'version')
    $sourceUrl = [string](Get-Property $Metadata 'sourceUrl')
    $uri = [Uri]$sourceUrl
    if ($uri.Scheme -ne 'https' -or $AllowedHosts -notcontains $uri.DnsSafeHost) {
        throw "$Name must identify an official HTTPS download URL."
    }
    if ($Name -eq 'dotnet' -or $Name -eq 'aspnetcore') {
        $algorithm = 'SHA512'
        $expected = [string](Get-Property $Metadata 'sha512')
    } else {
        $algorithm = [string](Get-Property $Metadata 'checksumAlgorithm')
        $expected = [string](Get-Property $Metadata 'checksum')
    }
    if (@('SHA256', 'SHA512', 'MD5') -notcontains $algorithm.ToUpperInvariant()) {
        throw "Unsupported official checksum algorithm for $Name."
    }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm $algorithm).Hash
    if (-not [string]::Equals($actual, $expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name archive does not match its official checksum."
    }
    return [ordered]@{
        name = $Name
        version = $version
        sourceUrl = $sourceUrl
        officialChecksumAlgorithm = $algorithm.ToUpperInvariant()
        officialChecksum = $actual.ToLowerInvariant()
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Expand-SafeArchive {
    param([string]$Archive, [string]$Destination)
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $prefix = [IO.Path]::GetFullPath($Destination).TrimEnd('\') + '\'
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName.Replace('/', '\')
            if ($name.Contains(':') -or [IO.Path]::IsPathRooted($name)) {
                throw 'An archive contains an unsafe path.'
            }
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $name))
            if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'An archive entry escapes its destination.'
            }
            if (-not $seen.Add($target)) { throw 'An archive contains duplicate paths.' }
            if ([string]::IsNullOrEmpty($entry.Name)) {
                New-Item -ItemType Directory -Path $target -Force | Out-Null
            } else {
                New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
            }
        }
    } finally { $zip.Dispose() }
}

function Export-PublicFile {
    param([string]$Source, [string]$Destination)
    $lines = @(Read-Git @('show', "${PublicMasterSha}:$Source"))
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($Destination)) -Force | Out-Null
    [IO.File]::WriteAllText($Destination, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
}

function Export-LocalConfig {
    param([string]$Destination)
    Export-PublicFile 'AORebirth/Config/Config.example.xml' $Destination
    [xml]$config = Get-Content -LiteralPath $Destination -Raw
    $config.Config.ListenIP = '127.0.0.1'
    $config.Config.ZoneIP = '127.0.0.1'
    $config.Config.ChatIP = '127.0.0.1'
    $config.Save($Destination)
}

function Publish-Net10 {
    param([string]$Project, [string]$Destination, [string]$Name)
    $output = Join-Path $script:workRoot "compiled\$Name\"
    Invoke-Checked $script:dotnet @(
        'publish', (Join-Path $repositoryRoot $Project), '--configuration', 'Debug',
        '--runtime', 'win-x64', '--self-contained', 'false', '--output', $Destination,
        "-p:OutDir=$output", '-p:UseAppHost=true', '-p:DebugType=None', '-p:DebugSymbols=false',
        '-p:UseSharedCompilation=false', '-m:1', '-nr:false', '-v:minimal'
    )
}

function Build-Net48 {
    param([string]$Project, [string]$Destination)
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Invoke-Checked $script:msbuild @(
        (Join-Path $repositoryRoot $Project), '/t:Build', '/p:Configuration=Debug',
        "/p:OutDir=$($Destination.TrimEnd('\'))\", '/p:DebugType=None', '/p:DebugSymbols=false',
        '/m:1', '/nr:false', '/v:minimal'
    )
}

function Assert-BundledFrameworks {
    param([string]$PackageRoot)
    foreach ($file in Get-ChildItem -LiteralPath $PackageRoot -Filter '*.runtimeconfig.json' -Recurse) {
        $options = (Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json).runtimeOptions
        $frameworks = @()
        if ($null -ne $options.PSObject.Properties['framework']) { $frameworks += $options.framework }
        if ($null -ne $options.PSObject.Properties['frameworks']) { $frameworks += $options.frameworks }
        foreach ($framework in $frameworks) {
            $required = [Version]$framework.version
            $frameworkRoot = Join-Path $PackageRoot "runtime\shared\$($framework.name)"
            if (-not (Test-Path -LiteralPath $frameworkRoot -PathType Container)) {
                throw "The package needs bundled framework $($framework.name) $required."
            }
            $matches = @(Get-ChildItem -LiteralPath $frameworkRoot -Directory | Where-Object {
                $candidate = [Version]$_.Name
                $candidate.Major -eq $required.Major -and $candidate.Minor -eq $required.Minor -and $candidate -ge $required
            })
            if ($matches.Count -eq 0) { throw "Bundled $($framework.name) cannot satisfy $required." }
        }
    }
}

function Get-ToolingInventory {
    $files = @(Get-SafeFiles $PSScriptRoot | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    return @($files | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($repositoryRoot.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
}

Push-Location $repositoryRoot
try {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $PublicMasterSha = $PublicMasterSha.ToLowerInvariant()
    $knownCommit = [string](Read-Git @('rev-parse', '--verify', "${PublicMasterSha}^{commit}"))
    if ($knownCommit.Trim() -ne $PublicMasterSha) { throw 'Public-master source commit is unavailable.' }
    $sourcePaths = @('AORebirth', 'WindowsBuildNet10', 'SharedBuild', 'Tools/DatabaseMigrationTool', 'Tools/DatabasePreflight')
    $differences = @(Read-Git (@('diff', '--name-only', $PublicMasterSha, '--') + $sourcePaths))
    $untracked = @(Read-Git (@('ls-files', '--others', '--exclude-standard', '--') + $sourcePaths))
    if ($differences.Count -gt 0 -or $untracked.Count -gt 0) {
        throw 'Runtime/build source differs from the requested public-master SHA. Reconcile before packaging.'
    }
    $toolingSha = ([string](Read-Git @('rev-parse', 'HEAD'))).Trim()
    $toolingDirty = @(Read-Git @('status', '--porcelain', '--untracked-files=all', '--', 'Tools/WindowsLocalPackage')).Count -gt 0
    $toolingInventory = @(Get-ToolingInventory)
    $GameDataRoot = [IO.Path]::GetFullPath($GameDataRoot)
    $gameFiles = @(Get-SafeFiles $GameDataRoot)
    if ($gameFiles.Count -eq 0) { throw 'Full GameData cannot be empty.' }
    foreach ($required in @('items.dat', 'NpcTemplates.json', 'StartPackages.json', 'Teams\LevelEligibility.json')) {
        Require-File (Join-Path $GameDataRoot $required) | Out-Null
    }
    if (-not (Test-Path -LiteralPath (Join-Path $GameDataRoot 'Playfields') -PathType Container)) {
        throw 'Full GameData must include Playfields.'
    }
    foreach ($file in $gameFiles) {
        $relative = $file.FullName.Substring($GameDataRoot.TrimEnd('\').Length + 1)
        if ($relative -match '(^|[\\/])(\.git|captures?|db-backups?|logs?)([\\/]|$)' -or
            $file.Name -match '(^\.env($|\.)|\.pcap(ng)?$|\.sql$|\.dmp$|(^|\.)(secrets?|credentials?)(\.(json|xml|config|ini|txt|yaml|yml|env))?$|\.secrets$)') {
            throw 'GameData contains an excluded evidence, credential, or database file. Review it before packaging.'
        }
    }
    $RuntimeArchive = Require-File $RuntimeArchive
    $MySqlArchive = Require-File $MySqlArchive
    $metadata = Get-Content -LiteralPath (Require-File $DependencyMetadata) -Raw | ConvertFrom-Json
    $dependencies = @()
    $dependencies += Verify-Dependency $RuntimeArchive (Get-Property $metadata 'runtime') 'dotnet' @('builds.dotnet.microsoft.com', 'download.visualstudio.microsoft.com')
    $dependencies += Verify-Dependency $MySqlArchive (Get-Property $metadata 'mysql') 'mysql' @('dev.mysql.com', 'cdn.mysql.com')
    if ($AspNetRuntimeArchive) {
        $AspNetRuntimeArchive = Require-File $AspNetRuntimeArchive
        $dependencies += Verify-Dependency $AspNetRuntimeArchive (Get-Property $metadata 'aspnetRuntime') 'aspnetcore' @('builds.dotnet.microsoft.com', 'download.visualstudio.microsoft.com')
    }
    $ClientManifest = Require-File $ClientManifest
    [xml]$client = Get-Content -LiteralPath $ClientManifest -Raw
    $clientElement = $client.DocumentElement
    $clientLength = [long]0
    if ($null -eq $clientElement -or $clientElement.Name -ne 'Client' -or
        [string]::IsNullOrWhiteSpace($clientElement.GetAttribute('downloadUrl')) -or
        [string]::IsNullOrWhiteSpace($clientElement.GetAttribute('executable')) -or
        $clientElement.GetAttribute('executableSha256') -notmatch '^[0-9a-fA-F]{64}$' -or
        -not [long]::TryParse($clientElement.GetAttribute('archiveLength'), [ref]$clientLength) -or $clientLength -le 0) {
        throw 'Client manifest must identify the approved fixed download, length, executable and executable SHA-256.'
    }
    $clientUri = [Uri]$clientElement.GetAttribute('downloadUrl')
    if ($clientUri.Scheme -ne 'https' -or $clientUri.DnsSafeHost -ne 'ao-rebirth.com') {
        throw 'Client download must use the approved AORebirth HTTPS website.'
    }
    $clientArchiveHash = $clientElement.GetAttribute('archiveSha256')
    if ($clientArchiveHash -and $clientArchiveHash -notmatch '^[0-9a-fA-F]{64}$') { throw 'Client archive SHA-256 is invalid.' }
    if ($VcRedistributable) {
        $VcRedistributable = Require-File $VcRedistributable
        $dependencies += Verify-Dependency $VcRedistributable (Get-Property $metadata 'vcRedist') 'vcRedist' @('aka.ms', 'download.visualstudio.microsoft.com')
        $signature = Get-AuthenticodeSignature -LiteralPath $VcRedistributable
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
            throw 'The VC++ prerequisite must have a valid Microsoft signature.'
        }
    }
    $vswhere = Require-File (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe')
    $msbuild = (& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1)
    if ($LASTEXITCODE -ne 0 -or -not $msbuild) { throw 'Visual Studio MSBuild is required on the package producer.' }
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source

    $base = Join-Path $repositoryRoot 'build-verify\windows-local-package'
    $ignored = & git -C $repositoryRoot check-ignore --quiet -- 'build-verify/windows-local-package/package.zip'
    if ($LASTEXITCODE -ne 0) { throw 'Package output must remain Git-ignored.' }
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
    $workRoot = Join-Path $base ($PublicMasterSha.Substring(0, 12) + '-' + $stamp + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $packageRoot = Join-Path $workRoot 'AORebirth'
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    Write-Host "Packaging public master $PublicMasterSha"
    if (-not $SkipCoreBuild) { Invoke-Checked 'cmd.exe' @('/d', '/c', 'tools\build_aorebirth_debug.cmd') }

    $engines = @(
        @{ Name = 'LoginEngine'; Project = 'WindowsBuildNet10\Projects\LoginEngine.WinNet10.csproj' },
        @{ Name = 'ChatEngine'; Project = 'WindowsBuildNet10\Projects\ChatEngine.WinNet10.csproj' },
        @{ Name = 'ZoneEngine_New'; Project = 'AORebirth\Server\ZoneEngine_New\ZoneEngine_New.csproj' }
    )
    foreach ($engine in $engines) {
        $destination = Join-Path $packageRoot "server\$($engine.Name)"
        Publish-Net10 $engine.Project $destination $engine.Name
        Require-File (Join-Path $destination "$($engine.Name).exe") | Out-Null
        Remove-StagingDirectory (Join-Path $destination 'GameData')
        Export-LocalConfig (Join-Path $destination 'Config.xml')
    }
    Publish-Net10 'Tools\DatabaseMigrationTool\DatabaseMigrationTool.csproj' (Join-Path $packageRoot 'tools\DatabaseMigrationTool') 'DatabaseMigrationTool'
    Publish-Net10 'Tools\WindowsLocalPackage\DatabaseSetup\DatabaseSetup.csproj' (Join-Path $packageRoot 'tools\DatabaseSetup') 'DatabaseSetup'
    Build-Net48 'Tools\DatabasePreflight\DatabasePreflight.csproj' (Join-Path $packageRoot 'tools\DatabasePreflight')
    Build-Net48 'AORebirth\Server\AccountBrokerService\AORebirth.AccountBroker.Service.csproj' (Join-Path $packageRoot 'tools\AccountBroker')
    $launcherOutput = Join-Path $workRoot 'launcher'
    Build-Net48 'Tools\WindowsLocalPackage\Launcher\AORebirth.WindowsLocalLauncher.csproj' $launcherOutput
    Copy-Tree $launcherOutput $packageRoot
    foreach ($helper in @('DatabasePreflight', 'AccountBroker')) {
        Remove-StagingDirectory (Join-Path $packageRoot "tools\$helper\GameData")
        Export-LocalConfig (Join-Path $packageRoot "tools\$helper\Config.xml")
    }

    Copy-Tree $GameDataRoot (Join-Path $packageRoot 'GameData')
    Require-File (Join-Path $packageRoot 'GameData\nanos.dat') | Out-Null
    Export-LocalConfig (Join-Path $packageRoot 'config\Config.xml')
    Copy-Item -LiteralPath $ClientManifest -Destination (Join-Path $packageRoot 'config\client.xml')
    $readme = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Raw
    $producerSection = $readme.IndexOf('## Producing a package', [StringComparison]::Ordinal)
    if ($producerSection -lt 0) { throw 'The package README is missing its player/producer boundary.' }
    [IO.File]::WriteAllText((Join-Path $packageRoot 'README.txt'), $readme.Substring(0, $producerSection).TrimEnd() + "`r`n", (New-Object Text.UTF8Encoding $false))
    foreach ($notice in @('LICENSE', 'NOTICE')) { Export-PublicFile $notice (Join-Path $packageRoot $notice) }
    Export-PublicFile 'AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/COPYING' (Join-Path $packageRoot 'licenses\AOtomation-COPYING')
    Export-PublicFile 'AORebirth/Libraries/Source/msgpack-cli/LICENSE.txt' (Join-Path $packageRoot 'licenses\MsgPack-LICENSE.txt')

    Expand-SafeArchive $RuntimeArchive (Join-Path $packageRoot 'runtime')
    if ($AspNetRuntimeArchive) {
        $aspNetExpanded = Join-Path $workRoot 'aspnet-runtime'
        Expand-SafeArchive $AspNetRuntimeArchive $aspNetExpanded
        Copy-Tree $aspNetExpanded (Join-Path $packageRoot 'runtime')
    }
    Require-File (Join-Path $packageRoot 'runtime\dotnet.exe') | Out-Null
    $mySqlExpanded = Join-Path $workRoot 'mysql-expanded'
    Expand-SafeArchive $MySqlArchive $mySqlExpanded
    $mySqlRoots = @(Get-ChildItem -LiteralPath $mySqlExpanded -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin\mysqld.exe') -PathType Leaf })
    if ($mySqlRoots.Count -ne 1) { throw 'Expected exactly one upstream MySQL binary directory.' }
    $mySqlSource = $mySqlRoots[0].FullName
    foreach ($directory in @('bin', 'share')) {
        Copy-Tree (Join-Path $mySqlSource $directory) (Join-Path $packageRoot "mysql\$directory")
    }
    $plugins = Join-Path $mySqlSource 'lib\plugin'
    if (Test-Path -LiteralPath $plugins -PathType Container) { Copy-Tree $plugins (Join-Path $packageRoot 'mysql\lib\plugin') }
    foreach ($notice in Get-ChildItem -LiteralPath $mySqlSource -File | Where-Object { $_.Name -match '^(LICENSE|COPYING|README|NOTICE)' }) {
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $packageRoot "mysql\$($notice.Name)")
    }
    foreach ($required in @('mysqld.exe', 'mysql.exe', 'mysqladmin.exe', 'mysqldump.exe')) { Require-File (Join-Path $packageRoot "mysql\bin\$required") | Out-Null }
    if ($VcRedistributable) {
        New-Item -ItemType Directory -Path (Join-Path $packageRoot 'prerequisites') -Force | Out-Null
        Copy-Item -LiteralPath $VcRedistributable -Destination (Join-Path $packageRoot 'prerequisites\vc_redist.x64.exe')
    }
    Assert-BundledFrameworks $packageRoot
    foreach ($required in @(
        'AORebirth.exe', 'prerequisites\vc_redist.x64.exe', 'tools\DatabaseSetup\DatabaseSetup.dll', 'tools\DatabaseMigrationTool\DatabaseMigrationTool.dll',
        'tools\DatabasePreflight\DatabasePreflight.exe', 'tools\AccountBroker\AORebirth.AccountBroker.Service.exe',
        'server\ZoneEngine_New\Config\Gameplay.json', 'server\ZoneEngine_New\Config\NavAgent.json',
        'server\ZoneEngine_New\XML Data\MissionRewards\ItemDb_Clusters.json',
        'server\ZoneEngine_New\Content\Runtime\interactions.json'
    )) { Require-File (Join-Path $packageRoot $required) | Out-Null }

    foreach ($pdb in Get-ChildItem -LiteralPath $packageRoot -Filter '*.pdb' -File -Recurse) { Remove-Item -LiteralPath $pdb.FullName }
    $packageFiles = @(Get-SafeFiles $packageRoot)
    foreach ($file in $packageFiles) {
        $relative = $file.FullName.Substring($packageRoot.Length + 1)
        if ($relative -match '(^|[\\/])(\.git|LinuxBuild|captures?|db-backups?|obj)([\\/]|$)' -or
            $file.Name -match '\.(cs|csproj|sln|props|targets|pcap|pcapng|dmp)$' -or
            $file.Name -match '(^\.env($|\.)|(^|\.)(secrets?|credentials?)(\.(json|xml|config|ini|txt|yaml|yml|env))?$|\.secrets$)') {
            throw "Excluded source, evidence, or local configuration file in package: $relative"
        }
    }
    $currentToolingInventory = @(Get-ToolingInventory)
    if (($toolingInventory | ConvertTo-Json -Depth 5 -Compress) -ne ($currentToolingInventory | ConvertTo-Json -Depth 5 -Compress)) {
        throw 'Packaging/launcher/helper source changed during the build. Rebuild after source edits finish.'
    }
    $finalSourceDifferences = @(Read-Git (@('diff', '--name-only', $PublicMasterSha, '--') + $sourcePaths))
    $finalUntrackedSources = @(Read-Git (@('ls-files', '--others', '--exclude-standard', '--') + $sourcePaths))
    if ($finalSourceDifferences.Count -gt 0 -or $finalUntrackedSources.Count -gt 0) {
        throw 'Authoritative runtime/build source changed during packaging.'
    }
    $provenance = [ordered]@{
        schemaVersion = 1
        publicMasterSha = $PublicMasterSha
        packageToolingSha = $toolingSha
        packageToolingDirty = $toolingDirty
        architecture = 'win-x64'
        generatedUtc = (Get-Date).ToUniversalTime().ToString('o')
        coreBuildSkipped = [bool]$SkipCoreBuild
        dependencies = $dependencies
        toolingFiles = $toolingInventory
        validation = 'BUILD_AND_PACKAGE_ONLY; CLEAN_MACHINE_AND_LIVE_CLIENT_ACCEPTANCE_UNVERIFIED'
    }
    $provenance | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $packageRoot 'provenance.json') -Encoding UTF8
    $inventory = @(Get-SafeFiles $packageRoot | Sort-Object FullName | ForEach-Object {
        [PSCustomObject][ordered]@{ path = $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/'); length = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    $inventory | Export-Csv -LiteralPath (Join-Path $packageRoot 'inventory.csv') -NoTypeInformation -Encoding UTF8
    $inventoryFile = Get-Item -LiteralPath (Join-Path $packageRoot 'inventory.csv')
    $manifestFiles = @($inventory) + @([PSCustomObject]@{ path = 'inventory.csv'; length = $inventoryFile.Length; sha256 = (Get-FileHash -LiteralPath $inventoryFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant() })
    $manifest = [ordered]@{
        schemaVersion = 1
        publicMasterSha = $PublicMasterSha
        packageToolingSha = $toolingSha
        packageToolingDirty = $toolingDirty
        architecture = 'win-x64'
        generatedUtc = $provenance.generatedUtc
        dependencies = $dependencies
        files = $manifestFiles
    }
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $packageRoot 'package-manifest.json') -Encoding UTF8
    $zipPath = Join-Path $workRoot ('AORebirth-Windows-x64-' + $PublicMasterSha.Substring(0, 12) + '.zip')
    [IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
    $zipSha = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(($zipPath + '.sha256'), ($zipSha + '  ' + [IO.Path]::GetFileName($zipPath) + "`n"))
    Write-Host "PUBLIC_MASTER_SHA=$PublicMasterSha"
    Write-Host "PACKAGE_TOOLING_SHA=$toolingSha"
    Write-Host "PACKAGE_TOOLING_DIRTY=$toolingDirty"
    Write-Host "PACKAGE_SHA256=$zipSha"
    Write-Host "PACKAGE=$zipPath"
    Write-Host 'CLEAN_MACHINE_ACCEPTANCE=UNVERIFIED'
} finally { Pop-Location }
