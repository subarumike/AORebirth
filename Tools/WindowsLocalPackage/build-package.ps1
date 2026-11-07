[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$PublicMasterSha,
    [Parameter(Mandatory = $true)][string]$GameDataRoot,
    [Parameter(Mandatory = $true)][string]$RuntimeArchive,
    [Parameter(Mandatory = $true)][string]$PhpArchive,
    [Parameter(Mandatory = $true)][string]$MySqlArchive,
    [Parameter(Mandatory = $true)][string]$DependencyMetadata,
    [Parameter(Mandatory = $true)][string]$ClientManifest,
    [Parameter(Mandatory = $true)][string]$BuildReceipt,
    [Parameter(Mandatory = $true)][string]$ContentIntakeReceipt,
    [Parameter(Mandatory = $true)][string]$GameplayConfig,
    [switch]$EnableHalloween,
    [string]$AspNetRuntimeArchive,
    [Parameter(Mandatory = $true)][string]$VcRedistributable
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

function Assert-PinnedPhpInventory {
    param([object[]]$Inventory, [System.Xml.XmlElement]$Definition)
    $expected = @{}
    foreach ($record in $Definition.SelectNodes('File')) {
        $relative = $record.GetAttribute('Path')
        if ($expected.ContainsKey($relative)) { throw 'The pinned PHP file inventory contains a duplicate path.' }
        $expected[$relative] = $record
    }
    $staged = @($Inventory | Where-Object { $_.path.StartsWith('php/', [StringComparison]::Ordinal) })
    if ($staged.Count -ne [int]$Definition.GetAttribute('FileCount') -or $staged.Count -ne $expected.Count) {
        throw 'The staged PHP file count differs from the official runtime manifest.'
    }
    $total = [long]0
    foreach ($entry in $staged) {
        $relative = $entry.path.Substring(4)
        $record = $expected[$relative]
        if ($null -eq $record -or $record.GetAttribute('Path') -cne $relative -or
            [long]$record.GetAttribute('Size') -ne $entry.length -or $record.GetAttribute('Sha256') -cne $entry.sha256) {
            throw "Staged PHP runtime file differs from its official manifest: $relative"
        }
        $total += $entry.length
        $expected.Remove($relative)
    }
    if ($expected.Count -ne 0 -or $total -ne [long]$Definition.GetAttribute('TotalUncompressedBytes')) {
        throw 'The staged PHP runtime inventory is incomplete.'
    }
}

function Export-PublicFile {
    param([string]$Source, [string]$Destination)
    $lines = @(Read-Git @('show', "${PublicMasterSha}:$Source"))
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($Destination)) -Force | Out-Null
    [IO.File]::WriteAllText($Destination, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
}

function Export-PublicBlob {
    param([string]$Source, [string]$Blob, [string]$Destination)
    if ($Source -notmatch '^[A-Za-z0-9._/-]+$' -or $Blob -notmatch '^[0-9a-f]{40}$') {
        throw 'An authoritative runtime asset has an unsafe Git object identity.'
    }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($Destination)) -Force | Out-Null
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = (Get-Command git -CommandType Application -ErrorAction Stop).Source
    $start.Arguments = 'cat-file blob ' + $Blob
    $start.WorkingDirectory = $repositoryRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    $stream = $null
    try {
        if (-not $process.Start()) { throw 'Cannot read an authoritative Git runtime asset.' }
        $stream = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        $process.StandardOutput.BaseStream.CopyTo($stream)
        $stream.Dispose()
        $stream = $null
        $diagnostic = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Cannot export the authoritative runtime asset: $Source" }
    } finally {
        if ($null -ne $stream) { $stream.Dispose() }
        $process.Dispose()
    }
    $actualBlob = ([string](Read-Git @('hash-object', '--no-filters', '--', $Destination))).Trim()
    if ($actualBlob -cne $Blob) { throw "Exported runtime asset differs from its public Git blob: $Source" }
}

function Export-LocalConfig {
    param([string]$Destination)
    Export-PublicFile 'AORebirth/Config/Config.example.xml' $Destination
    [xml]$config = Get-Content -LiteralPath $Destination -Raw
    $config.Config.ListenIP = '127.0.0.1'
    $config.Config.ZoneIP = '127.0.0.1'
    $config.Config.ChatIP = '127.0.0.1'
    $world = $config.Config.SelectSingleNode('World')
    if ($null -eq $world) { $world = $config.Config.AppendChild($config.CreateElement('World')) }
    foreach ($setting in @{
        ListenIP = '127.0.0.1'; Port = '7600'; StatusPort = '7601';
        Url = 'http://127.0.0.1:7600'; InstanceName = 'zone-7501'
    }.GetEnumerator()) {
        $node = $world.SelectSingleNode($setting.Key)
        if ($null -eq $node) { $node = $world.AppendChild($config.CreateElement($setting.Key)) }
        $node.InnerText = $setting.Value
    }
    $config.Save($Destination)
}

function Get-SafeChildPath {
    param([string]$Root, [string]$Relative)
    if ([string]::IsNullOrWhiteSpace($Relative) -or $Relative.Contains(':') -or
        [IO.Path]::IsPathRooted($Relative) -or ($Relative.Replace('\', '/').Split('/') -contains '..')) {
        throw "Unsafe package input path: $Relative"
    }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $target = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package input escapes its owning directory: $Relative"
    }
    $cursor = $target
    while ($cursor.Length -ge $prefix.TrimEnd('\').Length) {
        if ((Test-Path -LiteralPath $cursor) -and
            ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Reparse points cannot be packaged: $cursor"
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
        if ([string]::IsNullOrEmpty($cursor)) { break }
    }
    return $target
}

function Copy-VerifiedFile {
    param([string]$Source, [string]$Destination, [string]$ExpectedHash, [long]$ExpectedSize = -1)
    $Source = Require-File $Source
    $sourceFile = Get-Item -LiteralPath $Source
    if ($ExpectedSize -ge 0 -and $sourceFile.Length -ne $ExpectedSize) {
        throw "Measured input length changed: $Source"
    }
    if (-not $ExpectedHash) { $ExpectedHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($Destination)) -Force | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination
    if ((Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash -ne $ExpectedHash -or
        (Get-Item -LiteralPath $Destination).Length -ne $sourceFile.Length) {
        throw "Staged file does not match its measured input: $Source"
    }
}

function Copy-Net10Output {
    param([string]$SourceRoot, [string]$Destination, [string]$Name)
    # SDK build outputs are already runnable. Copy only the runtime dependency
    # closure described by their actual .deps.json; never republish product code.
    $depsPath = Get-SafeChildPath $SourceRoot "$Name.deps.json"
    $deps = Get-Content -LiteralPath (Require-File $depsPath) -Raw | ConvertFrom-Json
    $target = $deps.targets.PSObject.Properties[$deps.runtimeTarget.name]
    if ($null -eq $target) { throw "Missing runtime target in $Name.deps.json." }
    $assets = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($leaf in @("$Name.exe", "$Name.dll", "$Name.deps.json", "$Name.runtimeconfig.json")) { [void]$assets.Add($leaf) }
    if (Test-Path -LiteralPath (Join-Path $SourceRoot "$Name.exe.config") -PathType Leaf) { [void]$assets.Add("$Name.exe.config") }
    foreach ($library in $target.Value.PSObject.Properties) {
        foreach ($kind in @('runtime', 'native', 'resources', 'runtimeTargets')) {
            $section = $library.Value.PSObject.Properties[$kind]
            if ($null -eq $section) { continue }
            foreach ($asset in $section.Value.PSObject.Properties) {
                if ($asset.Name.EndsWith('/_._', [StringComparison]::Ordinal)) { continue }
                if ($kind -eq 'runtimeTargets') {
                    if ([string]$asset.Value.rid -notmatch '^win(-x64)?$') { continue }
                    $relative = $asset.Name
                } elseif ($kind -eq 'resources') {
                    $relative = [string]$asset.Value.locale + '/' + [IO.Path]::GetFileName($asset.Name)
                } else { $relative = [IO.Path]::GetFileName($asset.Name) }
                [void]$assets.Add($relative)
            }
        }
    }
    foreach ($relative in $assets) {
        $source = Get-SafeChildPath $SourceRoot $relative
        $targetPath = Get-SafeChildPath $Destination $relative
        $hash = (Get-FileHash -LiteralPath (Require-File $source) -Algorithm SHA256).Hash.ToLowerInvariant()
        Copy-VerifiedFile $source $targetPath $hash
        $script:builtOutputInventory += [ordered]@{
            source = $source.Substring($repositoryRoot.Length + 1).Replace('\', '/')
            target = $targetPath.Substring($packageRoot.Length + 1).Replace('\', '/')
            sha256 = $hash
        }
    }
}

function Build-Net10Tool {
    param([string]$Project, [string]$Destination)
    Invoke-Checked $env:ComSpec @(
        '/d', '/c', (Join-Path $repositoryRoot 'tools\windows_release_contract.cmd'),
        'run-feature-validation', '--', $script:dotnet, 'build', $Project.Replace('\', '/'),
        '--configuration', 'Debug', '--output', $Destination,
        '-p:UseAppHost=true', '-p:DebugType=None', '-p:DebugSymbols=false',
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
    foreach ($relative in @('tools/windows_release_contract.py', 'Tools/tests/test_windows_package_feature_validation.py')) {
        $path = Get-SafeChildPath $repositoryRoot $relative
        if (Test-Path -LiteralPath $path -PathType Leaf) { $files += Get-Item -LiteralPath $path }
    }
    return @($files | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($repositoryRoot.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
}

Push-Location $repositoryRoot
try {
    $phaseClock = [Diagnostics.Stopwatch]::StartNew()
    $phaseTimings = [ordered]@{}
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $PublicMasterSha = $PublicMasterSha.ToLowerInvariant()
    $knownCommit = [string](Read-Git @('rev-parse', '--verify', "${PublicMasterSha}^{commit}"))
    if ($knownCommit.Trim() -ne $PublicMasterSha) { throw 'Public-master source commit is unavailable.' }
    $sourcePaths = @('AORebirth', 'WindowsBuildNet10', 'SharedBuild', 'NewZoneEngineBuild',
        'Directory.Build.props', 'Directory.Build.targets', 'Tools/DatabaseMigrationTool', 'Tools/DatabasePreflight',
        ':!AORebirth/Config/Config.xml', ':!AORebirth/Config/ChatEngine.Config.xml')
    $differences = @(Read-Git (@('diff', '--name-only', $PublicMasterSha, '--') + $sourcePaths))
    $untracked = @(Read-Git (@('ls-files', '--others', '--exclude-standard', '--') + $sourcePaths))
    if ($differences.Count -gt 0 -or $untracked.Count -gt 0) {
        throw 'Runtime/build source differs from the requested public-master SHA. Reconcile before packaging.'
    }
    $toolingSha = ([string](Read-Git @('rev-parse', 'HEAD'))).Trim()
    $branchChanges = @(Read-Git @('diff', '--name-only', $PublicMasterSha, 'HEAD', '--'))
    foreach ($changed in $branchChanges) {
        if ($changed -notmatch '^Tools/WindowsLocalPackage/' -and
            $changed -notmatch '^Tools/tests/test_windows_local_package[^/]*\.py$' -and
            $changed -cne 'tools/windows_release_contract.py' -and
            $changed -cne 'Tools/tests/test_windows_package_feature_validation.py' -and
            $changed -notmatch '^docs/[^\r\n]+\.(md|json)$') {
            throw "Packaging branch includes a change outside standalone tooling/tests/docs: $changed"
        }
    }
    $toolingDirty = @(Read-Git @('status', '--porcelain', '--untracked-files=all', '--', 'Tools/WindowsLocalPackage',
        'tools/windows_release_contract.py', 'Tools/tests/test_windows_package_feature_validation.py')).Count -gt 0
    $toolingInventory = @(Get-ToolingInventory)
    $GameDataRoot = [IO.Path]::GetFullPath($GameDataRoot)
    Get-SafeFiles $GameDataRoot | Out-Null
    $receiptRoot = Join-Path $repositoryRoot '.local\release-contract'
    $BuildReceipt = Require-File $BuildReceipt
    if (-not $BuildReceipt.StartsWith($receiptRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Use this checkout's own governed Windows build receipt."
    }
    Get-SafeChildPath $receiptRoot $BuildReceipt.Substring($receiptRoot.Length + 1) | Out-Null
    $buildReceiptHash = (Get-FileHash -LiteralPath $BuildReceipt -Algorithm SHA256).Hash.ToLowerInvariant()
    $receipt = Get-Content -LiteralPath $BuildReceipt -Raw | ConvertFrom-Json
    if ($receipt.PUBLIC_MASTER_SHA -ne $PublicMasterSha -or $receipt.LOCAL_WINDOWS_SOURCE_SHA -ne $PublicMasterSha -or
        $receipt.WINDOWS_BUILD -ne 'PASS' -or $receipt.WINDOWS_BUILD_EXIT_CODE -ne 0 -or
        $receipt.CONTENT_DUMP_SHA -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'A successful exact-source governed Windows build receipt is required.'
    }
    $gameManifestPath = Get-SafeChildPath ([IO.Path]::GetDirectoryName($BuildReceipt)) $receipt.CANONICAL_MANIFEST.PATH
    $gameManifestHash = (Get-FileHash -LiteralPath (Require-File $gameManifestPath) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($gameManifestHash -ne $receipt.CANONICAL_MANIFEST.SHA256 -or
        $gameManifestHash -ne $receipt.WINDOWS_GAMEDATA_MANIFEST) {
        throw 'The admitted GameData manifest does not match the Windows build receipt.'
    }
    # Windows PowerShell emits a top-level JSON array as one pipeline object.
    # Assign that value before normalizing it, so @() cannot nest the entire
    # manifest and turn member enumeration into one concatenated file path.
    $gameManifest = Get-Content -LiteralPath $gameManifestPath -Raw | ConvertFrom-Json
    $gameManifest = @($gameManifest)
    if ($gameManifest.Count -eq 0) { throw 'Admitted runtime GameData cannot be empty.' }
    $ContentIntakeReceipt = Require-File $ContentIntakeReceipt
    if (-not $ContentIntakeReceipt.StartsWith($receiptRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Use a fresh governed content intake from this owning checkout.'
    }
    Get-SafeChildPath $receiptRoot $ContentIntakeReceipt.Substring($receiptRoot.Length + 1) | Out-Null
    $intake = Get-Content -LiteralPath $ContentIntakeReceipt -Raw | ConvertFrom-Json
    if ($intake.PUBLIC_MASTER_SHA -ne $PublicMasterSha -or $intake.LOCAL_WINDOWS_SOURCE_SHA -ne $PublicMasterSha -or
        $intake.CONTENT_DUMP_SHA -ne $receipt.CONTENT_DUMP_SHA -or $intake.WINDOWS_GAMEDATA_MANIFEST -ne $gameManifestHash) {
        throw 'Fresh source/content intake changed the successful build tuple; rebuild those inputs before packaging.'
    }
    $intakeManifest = Get-SafeChildPath ([IO.Path]::GetDirectoryName($ContentIntakeReceipt)) $intake.CANONICAL_MANIFEST.PATH
    if ((Get-FileHash -LiteralPath (Require-File $intakeManifest) -Algorithm SHA256).Hash -ne $gameManifestHash) {
        throw 'The fresh intake manifest differs from the measured Windows build.'
    }
    $intakeReceiptHash = (Get-FileHash -LiteralPath $ContentIntakeReceipt -Algorithm SHA256).Hash.ToLowerInvariant()
    $gamePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $gameManifest) {
        if ($entry.path -isnot [string]) { throw 'Each admitted GameData manifest entry must name one string path.' }
        $relative = [string]$entry.path
        if (-not $gamePaths.Add($relative) -or $entry.sha256 -notmatch '^[0-9a-fA-F]{64}$' -or $entry.size -lt 0) {
            throw 'The admitted GameData manifest contains invalid or duplicate entries.'
        }
        Get-SafeChildPath $GameDataRoot $relative | Out-Null
    }
    $expectedBinaries = @(
        'AORebirth/Built/Debug/ChatEngine.exe', 'AORebirth/Built/Debug/ChatEngine.dll',
        'AORebirth/Built/Debug/LoginEngine.exe', 'AORebirth/Built/Debug/LoginEngine.dll',
        'AORebirth/Built/Debug/WorldEngine/WorldEngine.exe', 'AORebirth/Built/Debug/WorldEngine/WorldEngine.dll',
        'AORebirth/Built/Debug/ZoneEngine_New/ZoneEngine_New.exe', 'AORebirth/Built/Debug/ZoneEngine_New/ZoneEngine_New.dll'
    )
    foreach ($relative in $expectedBinaries) {
        $observations = @($receipt.BINARIES | Where-Object { $_.path -ceq $relative })
        if ($observations.Count -ne 1) { throw "The governed build did not measure $relative." }
        $observation = $observations[0]
        $binary = Get-SafeChildPath $repositoryRoot $relative
        if ((Get-Item -LiteralPath (Require-File $binary)).Length -ne $observation.bytes -or
            (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash -ne $observation.sha256) {
            throw "The built engine differs from its governed receipt: $relative"
        }
    }
    $GameplayConfig = Require-File $GameplayConfig
    Get-SafeChildPath ([IO.Path]::GetDirectoryName($GameplayConfig)) ([IO.Path]::GetFileName($GameplayConfig)) | Out-Null
    $gameplayInputHash = (Get-FileHash -LiteralPath $GameplayConfig -Algorithm SHA256).Hash.ToLowerInvariant()
    foreach ($required in @('items.dat', 'NpcTemplates.json', 'StartPackages.json', 'Teams\LevelEligibility.json')) {
        Require-File (Join-Path $GameDataRoot $required) | Out-Null
    }
    if (-not (Test-Path -LiteralPath (Join-Path $GameDataRoot 'Playfields') -PathType Container)) {
        throw 'Full GameData must include Playfields.'
    }
    foreach ($entry in $gameManifest) {
        $relative = [string]$entry.path
        $fileName = [IO.Path]::GetFileName($relative)
        if ($relative -match '(^|[\\/])(\.git|captures?|db-backups?|logs?)([\\/]|$)' -or
            $fileName -match '(^\.env($|\.)|\.pcap(ng)?$|\.sql$|\.dmp$|(^|\.)(secrets?|credentials?)(\.(json|xml|config|ini|txt|yaml|yml|env))?$|\.secrets$)') {
            throw 'GameData contains an excluded evidence, credential, or database file. Review it before packaging.'
        }
    }
    $RuntimeArchive = Require-File $RuntimeArchive
    $MySqlArchive = Require-File $MySqlArchive
    $metadata = Get-Content -LiteralPath (Require-File $DependencyMetadata) -Raw | ConvertFrom-Json
    $dependencies = @()
    $dependencies += Verify-Dependency $RuntimeArchive (Get-Property $metadata 'runtime') 'dotnet' @('builds.dotnet.microsoft.com', 'download.visualstudio.microsoft.com')
    $dependencies += Verify-Dependency $MySqlArchive (Get-Property $metadata 'mysql') 'mysql' @('dev.mysql.com', 'cdn.mysql.com')
    # Keep the producer on the exact public source's existing official PHP pin.
    # Do not fetch during a package build or read a mutable installed runtime.
    $phpManifestSource = 'AORebirth/Config/PhpRuntime.manifest.xml'
    $phpManifestBlob = ([string](Read-Git @('rev-parse', '--verify', "${PublicMasterSha}:$phpManifestSource"))).Trim()
    [xml]$phpManifest = ((Read-Git @('show', "${PublicMasterSha}:$phpManifestSource")) -join "`n")
    $phpDefinition = $phpManifest.DocumentElement
    if ($phpDefinition.Name -cne 'PhpRuntimeManifest' -or $phpDefinition.GetAttribute('SchemaVersion') -cne '1' -or
        $phpDefinition.GetAttribute('Architecture') -cne 'x64' -or $phpDefinition.GetAttribute('ThreadSafety') -cne 'NTS' -or
        $phpDefinition.GetAttribute('Toolchain') -cne 'VS17' -or $phpDefinition.GetAttribute('ArchiveRoot') -cne 'flat' -or
        $phpDefinition.GetAttribute('Version') -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$' -or
        $phpDefinition.GetAttribute('ArchiveSha256') -notmatch '^[0-9a-f]{64}$') {
        throw 'The public PHP runtime pin does not describe a supported official Windows x64 NTS archive.'
    }
    $PhpArchive = Require-File $PhpArchive
    $phpArchiveInfo = Get-Item -LiteralPath $PhpArchive
    $phpArchiveName = $phpDefinition.GetAttribute('ArchiveFilename')
    $phpOfficialUri = [Uri]$phpDefinition.GetAttribute('OfficialUrl')
    if ($phpArchiveInfo.Name -cne $phpArchiveName -or $phpArchiveInfo.Length -ne [long]$phpDefinition.GetAttribute('ArchiveSize') -or
        -not $phpOfficialUri.AbsolutePath.EndsWith('/' + $phpArchiveName, [StringComparison]::Ordinal)) {
        throw 'The supplied PHP archive filename or size does not match the exact public pin.'
    }
    $phpMetadata = [pscustomobject]@{
        version = $phpDefinition.GetAttribute('Version')
        sourceUrl = $phpOfficialUri.AbsoluteUri
        checksumAlgorithm = 'SHA256'
        checksum = $phpDefinition.GetAttribute('ArchiveSha256')
    }
    $phpDependency = Verify-Dependency $PhpArchive $phpMetadata 'php' @('downloads.php.net')
    $phpDependency['manifestSource'] = $phpManifestSource
    $phpDependency['manifestBlob'] = $phpManifestBlob
    $phpDependency['archiveBytes'] = $phpArchiveInfo.Length
    $phpDependency['architecture'] = $phpDefinition.GetAttribute('Architecture')
    $phpDependency['threadSafety'] = $phpDefinition.GetAttribute('ThreadSafety')
    $phpDependency['toolchain'] = $phpDefinition.GetAttribute('Toolchain')
    $dependencies += $phpDependency
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
    $phaseTimings['verifiedInputsAndDependencyArchivesSeconds'] = $phaseClock.Elapsed.TotalSeconds
    $phaseClock.Restart()

    $base = Join-Path $repositoryRoot 'build-verify\windows-local-package'
    $ignored = & git -C $repositoryRoot check-ignore --quiet -- 'build-verify/windows-local-package/package.zip'
    if ($LASTEXITCODE -ne 0) { throw 'Package output must remain Git-ignored.' }
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
    $workRoot = Join-Path $base ($PublicMasterSha.Substring(0, 12) + '-' + $stamp + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    $packageRoot = Join-Path $workRoot 'AORebirth'
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    Write-Host "Packaging public master $PublicMasterSha"
    $builtOutputInventory = @()
    $engines = @(
        @{ Name = 'LoginEngine'; Source = 'AORebirth\Built\Debug' },
        @{ Name = 'ChatEngine'; Source = 'AORebirth\Built\Debug' },
        @{ Name = 'WorldEngine'; Source = 'AORebirth\Built\Debug\WorldEngine' },
        @{ Name = 'ZoneEngine_New'; Source = 'AORebirth\Built\Debug\ZoneEngine_New' }
    )
    foreach ($engine in $engines) {
        $source = Join-Path $repositoryRoot $engine.Source
        $destination = Join-Path $packageRoot "server\$($engine.Name)"
        Copy-Net10Output $source $destination $engine.Name
        if ($engine.Name -eq 'WorldEngine' -or $engine.Name -eq 'ZoneEngine_New') {
            foreach ($directory in @('Config', 'Content', 'Migrations', 'SqlTables', 'XML Data')) {
                $sidecar = Join-Path $source $directory
                if (Test-Path -LiteralPath $sidecar -PathType Container) { Copy-Tree $sidecar (Join-Path $destination $directory) }
            }
        }
        if ($engine.Name -eq 'ChatEngine') {
            Export-PublicFile 'AORebirth/Config/ChatEngine.Config.xml' (Join-Path $destination 'ChatEngine.Config.xml')
        }
        Export-LocalConfig (Join-Path $destination 'Config.xml')
    }
    # The current content manifest loads its quest pack only. This authored
    # research summary is copied by the project's broad Content glob, but is
    # not a runtime input; retain its source and omit only this packaged copy.
    $excludedNonRuntimeFiles = @()
    $referenceRelative = 'server/ZoneEngine_New/Content/Nascence/life-dialogs/captures/20260822-221109.capture-reference.json'
    $referencePath = [IO.Path]::GetFullPath((Join-Path $packageRoot $referenceRelative))
    if (Test-Path -LiteralPath $referencePath -PathType Leaf) {
        $contentRoot = Join-Path $packageRoot 'server\ZoneEngine_New\Content\Nascence\life-dialogs'
        Get-SafeFiles $contentRoot | Out-Null
        $contentManifest = Get-Content -LiteralPath (Join-Path $contentRoot 'manifest.json') -Raw | ConvertFrom-Json
        foreach ($questPack in $contentManifest.QuestPacks) {
            if ([IO.Path]::GetFullPath((Join-Path $contentRoot $questPack)) -eq $referencePath) {
                throw 'The source-reference file became a runtime quest input; review the package exclusion.'
            }
        }
        if (-not $referencePath.StartsWith($workRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove a reference file outside package staging.'
        }
        Remove-Item -LiteralPath $referencePath
        $excludedNonRuntimeFiles += [ordered]@{ path = $referenceRelative; reason = 'Authored source-reference summary; not listed in runtime manifest QuestPacks.' }
    }
    Copy-Net10Output (Join-Path $repositoryRoot 'Tools\DatabaseMigrationTool\bin\Release\net10.0') (Join-Path $packageRoot 'tools\DatabaseMigrationTool') 'DatabaseMigrationTool'
    $setupOutput = Join-Path $workRoot 'compiled\DatabaseSetup'
    Build-Net10Tool 'Tools\WindowsLocalPackage\DatabaseSetup\DatabaseSetup.csproj' $setupOutput
    Copy-Net10Output $setupOutput (Join-Path $packageRoot 'tools\DatabaseSetup') 'DatabaseSetup'
    Copy-Tree (Join-Path $repositoryRoot 'AORebirth\Built\Debug\DatabasePreflight') (Join-Path $packageRoot 'tools\DatabasePreflight')
    Build-Net48 'AORebirth\Server\AccountBrokerService\AORebirth.AccountBroker.Service.csproj' (Join-Path $packageRoot 'tools\AccountBroker')
    $launcherOutput = Join-Path $workRoot 'launcher'
    Build-Net48 'Tools\WindowsLocalPackage\Launcher\AORebirth.WindowsLocalLauncher.csproj' $launcherOutput
    Copy-Tree $launcherOutput $packageRoot
    foreach ($helper in @('DatabasePreflight', 'AccountBroker')) {
        Remove-StagingDirectory (Join-Path $packageRoot "tools\$helper\GameData")
        Export-LocalConfig (Join-Path $packageRoot "tools\$helper\Config.xml")
    }
    $phaseTimings['engineAndToolStagingSeconds'] = $phaseClock.Elapsed.TotalSeconds
    $phaseClock.Restart()

    foreach ($entry in $gameManifest) {
        Copy-VerifiedFile (Get-SafeChildPath $GameDataRoot $entry.path) `
            (Get-SafeChildPath (Join-Path $packageRoot 'GameData') $entry.path) $entry.sha256 $entry.size
    }
    Copy-VerifiedFile $gameManifestPath (Join-Path $packageRoot 'runtime-content-manifest.json') $gameManifestHash
    $gameplayDestination = Join-Path $packageRoot 'server\ZoneEngine_New\Config\Gameplay.json'
    Copy-VerifiedFile $GameplayConfig $gameplayDestination $gameplayInputHash
    $gameplay = Get-Content -LiteralPath $gameplayDestination -Raw | ConvertFrom-Json
    if ($EnableHalloween) {
        $gameplay.events.halloween.enabled = $true
        $gameplay | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $gameplayDestination -Encoding UTF8
    }
    $gameplayHash = (Get-FileHash -LiteralPath $gameplayDestination -Algorithm SHA256).Hash.ToLowerInvariant()
    Require-File (Join-Path $packageRoot 'GameData\nanos.dat') | Out-Null
    Export-LocalConfig (Join-Path $packageRoot 'config\Config.xml')
    Copy-Item -LiteralPath $ClientManifest -Destination (Join-Path $packageRoot 'config\client.xml')
    # These entrypoints/configurations and their reviewed day-cell/button image
    # namespaces are consumed by the current public Daily frontend. Export Git
    # blobs directly: Windows PowerShell text redirection corrupts PNG bytes.
    $dailySourceRoot = 'tools-temp/icc-rk-local-web/daily'
    $dailyEntryPoints = @('index.app', 'index.html', 'claim.php', 'rewards.json', 'js/daily.js', 'css/icc.css')
    $dailyAssetInventory = @()
    foreach ($line in @(Read-Git @('ls-tree', '-r', $PublicMasterSha, '--', $dailySourceRoot))) {
        if ($line -notmatch '^(100644|100755) blob ([0-9a-f]{40})\t(.+)$') {
            throw 'The public Daily frontend contains a non-regular Git asset.'
        }
        $blob = $Matches[2]
        $sourcePath = $Matches[3]
        $relative = $sourcePath.Substring($dailySourceRoot.Length + 1)
        $runtimeAsset = $dailyEntryPoints -ccontains $relative -or
            $relative -cmatch '^assets/cells/(day[0-9]+-[0-9a-z-]+|taken)\.png$' -or
            $relative -cmatch '^assets/buttons/btn-[0-9]{2}\.png$' -or $relative -ceq 'assets/claim-reward.png'
        if (-not $runtimeAsset) {
            if ($relative -match '(^|/)(docs?|cache|reports?)(/|$)|(^|/)(README|LICENSE|NOTICE)(\.|$)|\.(md|log)$') {
                $excludedNonRuntimeFiles += [ordered]@{ path = $sourcePath; reason = 'Daily frontend documentation/cache/report; not a reviewed runtime asset.' }
                continue
            }
            throw "Unclassified public Daily frontend asset requires review: $relative"
        }
        $targetPath = Get-SafeChildPath (Join-Path $packageRoot 'web\daily') $relative
        Export-PublicBlob $sourcePath $blob $targetPath
        $dailyAssetInventory += [ordered]@{
            source = $sourcePath; blob = $blob
            target = 'web/daily/' + $relative
            sha256 = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash.ToLowerInvariant()
            bytes = (Get-Item -LiteralPath $targetPath).Length
        }
    }
    foreach ($relative in $dailyEntryPoints) { Require-File (Join-Path $packageRoot ('web\daily\' + $relative)) | Out-Null }
    Copy-VerifiedFile (Get-SafeChildPath $PSScriptRoot 'DailyWeb/router.php') (Join-Path $packageRoot 'web\daily-router.php')
    Copy-VerifiedFile (Get-SafeChildPath $PSScriptRoot 'DailyWeb/php.ini') (Join-Path $packageRoot 'web\daily.php.ini')
    $dailyRewards = Get-Content -LiteralPath (Join-Path $packageRoot 'web\daily\rewards.json') -Raw | ConvertFrom-Json
    $dailyImageReferences = @($dailyRewards.takenImage, $dailyRewards.claimButtonImage)
    $dailyDays = if ($dailyRewards.days -is [array]) {
        $dailyRewards.days
    } else {
        $dailyRewards.days.PSObject.Properties | ForEach-Object { $_.Value }
    }
    foreach ($day in $dailyDays) {
        foreach ($name in @('image', 'buttonImage')) {
            $property = $day.PSObject.Properties[$name]
            if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
                $dailyImageReferences += [string]$property.Value
            }
        }
    }
    $missingDailyImages = @($dailyImageReferences | Sort-Object -Unique | Where-Object {
        -not (Test-Path -LiteralPath (Get-SafeChildPath (Join-Path $packageRoot 'web\daily') $_) -PathType Leaf)
    })
    $readme = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Raw
    $producerSection = $readme.IndexOf('## Producing a package', [StringComparison]::Ordinal)
    if ($producerSection -lt 0) { throw 'The package README is missing its player/producer boundary.' }
    [IO.File]::WriteAllText((Join-Path $packageRoot 'README.txt'), $readme.Substring(0, $producerSection).TrimEnd() + "`r`n", (New-Object Text.UTF8Encoding $false))
    foreach ($notice in @('LICENSE', 'NOTICE')) { Export-PublicFile $notice (Join-Path $packageRoot $notice) }
    Export-PublicFile 'AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/COPYING' (Join-Path $packageRoot 'licenses\AOtomation-COPYING')
    Export-PublicFile 'AORebirth/Libraries/Source/msgpack-cli/LICENSE.txt' (Join-Path $packageRoot 'licenses\MsgPack-LICENSE.txt')
    $phaseTimings['admittedContentStagingSeconds'] = $phaseClock.Elapsed.TotalSeconds
    $phaseClock.Restart()

    Expand-SafeArchive $RuntimeArchive (Join-Path $packageRoot 'runtime')
    if ($AspNetRuntimeArchive) {
        $aspNetExpanded = Join-Path $workRoot 'aspnet-runtime'
        Expand-SafeArchive $AspNetRuntimeArchive $aspNetExpanded
        Copy-Tree $aspNetExpanded (Join-Path $packageRoot 'runtime')
    }
    Require-File (Join-Path $packageRoot 'runtime\dotnet.exe') | Out-Null
    Expand-SafeArchive $PhpArchive (Join-Path $packageRoot 'php')
    $packagedPhpManifest = Join-Path $packageRoot 'config\PhpRuntime.manifest.xml'
    Export-PublicBlob $phpManifestSource $phpManifestBlob $packagedPhpManifest
    $phpDependency['manifestSha256'] = (Get-FileHash -LiteralPath $packagedPhpManifest -Algorithm SHA256).Hash.ToLowerInvariant()
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
    $phaseTimings['dependencyArchiveExpansionSeconds'] = $phaseClock.Elapsed.TotalSeconds
    $phaseClock.Restart()
    foreach ($required in @(
        'AORebirth.exe', 'AORebirth.exe.config', 'prerequisites\vc_redist.x64.exe', 'tools\DatabaseSetup\DatabaseSetup.dll', 'tools\DatabaseMigrationTool\DatabaseMigrationTool.dll',
        'tools\DatabasePreflight\DatabasePreflight.exe', 'tools\AccountBroker\AORebirth.AccountBroker.Service.exe',
        'runtime-content-manifest.json', 'server\WorldEngine\WorldEngine.exe', 'server\WorldEngine\WorldEngine.dll',
        'server\WorldEngine\WorldEngine.deps.json', 'server\WorldEngine\WorldEngine.runtimeconfig.json',
        'config\PhpRuntime.manifest.xml', 'php\php.exe', 'php\php8.dll', 'php\ext\php_pdo_mysql.dll',
        'php\license.txt', 'php\readme-redist-bins.txt', 'web\daily-router.php', 'web\daily.php.ini',
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
    foreach ($observation in $receipt.BINARIES) {
        if ($expectedBinaries -notcontains $observation.path) { continue }
        if ((Get-FileHash -LiteralPath (Get-SafeChildPath $repositoryRoot $observation.path) -Algorithm SHA256).Hash -ne $observation.sha256) {
            throw "A governed engine binary changed during packaging: $($observation.path)"
        }
    }
    if ((Get-FileHash -LiteralPath $BuildReceipt -Algorithm SHA256).Hash -ne $buildReceiptHash -or
        (Get-FileHash -LiteralPath $ContentIntakeReceipt -Algorithm SHA256).Hash -ne $intakeReceiptHash -or
        (Get-FileHash -LiteralPath $gameManifestPath -Algorithm SHA256).Hash -ne $gameManifestHash -or
        (Get-FileHash -LiteralPath $GameplayConfig -Algorithm SHA256).Hash -ne $gameplayInputHash) {
        throw 'A measured package input changed during construction.'
    }
    $stagedGameRoot = Join-Path $packageRoot 'GameData'
    $stagedGameFiles = @(Get-SafeFiles $stagedGameRoot)
    if ($stagedGameFiles.Count -ne $gameManifest.Count) { throw 'Staged GameData differs from its complete admitted manifest.' }
    foreach ($file in $stagedGameFiles) {
        $relative = $file.FullName.Substring($stagedGameRoot.Length + 1).Replace('\', '/')
        if (-not $gamePaths.Contains($relative)) { throw "Unadmitted staged GameData: $relative" }
    }
    $provenance = [ordered]@{
        schemaVersion = 2
        publicMasterSha = $PublicMasterSha
        packageToolingSha = $toolingSha
        packageToolingDirty = $toolingDirty
        architecture = 'win-x64'
        generatedUtc = (Get-Date).ToUniversalTime().ToString('o')
        productBinariesReused = $true
        windowsBuildReceiptSha256 = $buildReceiptHash
        contentIntakeReceiptSha256 = $intakeReceiptHash
        contentDumpSha = $receipt.CONTENT_DUMP_SHA
        windowsGameDataManifestSha256 = $gameManifestHash
        windowsUserAccepted = $receipt.WINDOWS_USER_ACCEPTED
        governedEngineBinaries = $receipt.BINARIES
        builtOutputFiles = $builtOutputInventory
        gameplayConfiguration = [ordered]@{
            inputSha256 = $gameplayInputHash
            packagedSha256 = $gameplayHash
            halloweenEnabled = [bool]$gameplay.events.halloween.enabled
        }
        dailyFrontend = [ordered]@{
            sourcePublicMasterSha = $PublicMasterSha
            files = $dailyAssetInventory
            bundledLoopbackPhpHost = $true
            router = 'web/daily-router.php'
            ini = 'web/daily.php.ini'
            publicBaselineMissingReferencedImages = $missingDailyImages
            browserAcceptance = 'UNVERIFIED'
        }
        dependencies = $dependencies
        excludedNonRuntimeFiles = $excludedNonRuntimeFiles
        toolingFiles = $toolingInventory
        validation = 'BUILD_AND_PACKAGE_ONLY; CLEAN_MACHINE_AND_LIVE_CLIENT_ACCEPTANCE_UNVERIFIED'
    }
    $provenance | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $packageRoot 'provenance.json') -Encoding UTF8
    $inventory = @(Get-SafeFiles $packageRoot | Sort-Object FullName | ForEach-Object {
        [PSCustomObject][ordered]@{ path = $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/'); length = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    Assert-PinnedPhpInventory $inventory $phpDefinition
    $inventory | Export-Csv -LiteralPath (Join-Path $packageRoot 'inventory.csv') -NoTypeInformation -Encoding UTF8
    $inventoryFile = Get-Item -LiteralPath (Join-Path $packageRoot 'inventory.csv')
    $manifestFiles = @($inventory) + @([PSCustomObject]@{ path = 'inventory.csv'; length = $inventoryFile.Length; sha256 = (Get-FileHash -LiteralPath $inventoryFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant() })
    $manifest = [ordered]@{
        schemaVersion = 2
        publicMasterSha = $PublicMasterSha
        packageToolingSha = $toolingSha
        packageToolingDirty = $toolingDirty
        windowsBuildReceiptSha256 = $buildReceiptHash
        contentDumpSha = $receipt.CONTENT_DUMP_SHA
        windowsGameDataManifestSha256 = $gameManifestHash
        architecture = 'win-x64'
        generatedUtc = $provenance.generatedUtc
        dependencies = $dependencies
        files = $manifestFiles
    }
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $packageRoot 'package-manifest.json') -Encoding UTF8
    $verification = Start-Process -FilePath (Join-Path $packageRoot 'AORebirth.exe') -ArgumentList '--verify-package' `
        -WorkingDirectory $packageRoot -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $workRoot 'package-verification.stdout.log') `
        -RedirectStandardError (Join-Path $workRoot 'package-verification.stderr.log')
    if ($verification.ExitCode -ne 0) {
        throw "The staged launcher rejected this package (exit $($verification.ExitCode)). See package-verification logs in $workRoot."
    }
    $verification.Dispose()
    Write-Host 'STAGED_PACKAGE_VERIFICATION=PASS'
    $phaseTimings['inventoryAndReadOnlyVerificationSeconds'] = $phaseClock.Elapsed.TotalSeconds
    $phaseClock.Restart()
    $zipPath = Join-Path $workRoot ('AORebirth-Windows-x64-' + $PublicMasterSha.Substring(0, 12) + '.zip')
    [IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $zipPath, [IO.Compression.CompressionLevel]::Fastest, $true)
    $zipSha = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(($zipPath + '.sha256'), ($zipSha + '  ' + [IO.Path]::GetFileName($zipPath) + "`n"))
    $phaseTimings['zipCompressionAndDigestSeconds'] = $phaseClock.Elapsed.TotalSeconds
    [ordered]@{
        publicMasterSha = $PublicMasterSha
        windowsBuildReceiptSha256 = $buildReceiptHash
        windowsGameDataManifestSha256 = $gameManifestHash
        package = [IO.Path]::GetFileName($zipPath)
        packageSha256 = $zipSha
        dependencyCacheDecision = 'REUSED_VERIFIED_LOCAL_ARCHIVES'
        productBinaryCacheDecision = 'REUSED_EXACT_GOVERNED_WINDOWS_BUILD'
        phaseTimings = $phaseTimings
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $workRoot 'package-run.json') -Encoding UTF8
    Write-Host "PUBLIC_MASTER_SHA=$PublicMasterSha"
    Write-Host "PACKAGE_TOOLING_SHA=$toolingSha"
    Write-Host "PACKAGE_TOOLING_DIRTY=$toolingDirty"
    Write-Host "PACKAGE_SHA256=$zipSha"
    Write-Host "PACKAGE=$zipPath"
    Write-Host 'CLEAN_MACHINE_ACCEPTANCE=UNVERIFIED'
} catch {
    [Console]::Error.WriteLine('PACKAGER_ERROR_TYPE=' + $_.Exception.GetBaseException().GetType().FullName)
    foreach ($diagnostic in @($_.InvocationInfo.PositionMessage, $_.ScriptStackTrace)) {
        if (-not [string]::IsNullOrWhiteSpace($diagnostic)) {
            $bounded = [string]$diagnostic
            if ($bounded.Length -gt 2000) { $bounded = $bounded.Substring(0, 2000) + ' [truncated]' }
            [Console]::Error.WriteLine($bounded)
        }
    }
    throw
} finally { Pop-Location }
