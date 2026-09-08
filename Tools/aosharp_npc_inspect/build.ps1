param([Parameter(Mandatory = $true)][string]$AOSharpPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runtime = (Resolve-Path -LiteralPath $AOSharpPath).Path
$output = Join-Path $repo '.local\npc-inspect-probe'
$package = Join-Path $output 'package'
$intermediate = (Join-Path $output 'obj').Replace('\', '/') + '/'
$packageProperty = $package.Replace('\', '/') + '/'
New-Item -ItemType Directory -Path $package -Force | Out-Null
& dotnet build (Join-Path $PSScriptRoot 'NpcInspectProbe.csproj') --configuration Release `
    "-p:AOSharpPath=$runtime" "-p:BaseIntermediateOutputPath=$intermediate" `
    "-p:OutputPath=$packageProperty" '-p:AppendTargetFrameworkToOutputPath=false'
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $package 'README.md')
$manifest = foreach ($name in @('AOSharp.Core.dll', 'AOSharp.Common.dll')) {
    $file = Join-Path $runtime $name
    [pscustomobject]@{ Name = $name; SHA256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'build-runtime.json') -Encoding UTF8
Compress-Archive -LiteralPath (Join-Path $package 'NpcInspectProbe.dll'), (Join-Path $package 'README.md'), (Join-Path $package 'build-runtime.json') `
    -DestinationPath (Join-Path $output 'NpcInspectProbe.zip') -Force
Write-Host "Built standalone DLL: $package\NpcInspectProbe.dll"
Write-Host 'No runtime DLLs installed or modified. No client launched.'
