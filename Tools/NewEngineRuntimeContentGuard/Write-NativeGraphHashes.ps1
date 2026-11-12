param(
    [Parameter(Mandatory)][string]$Checkout,
    [Parameter(Mandatory)][string]$BuildRoot
)

$ErrorActionPreference = 'Stop'
$checkoutPrefix = [IO.Path]::GetFullPath($Checkout).TrimEnd('\') + '\'
$graph = Join-Path $BuildRoot 'graph'
$paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($group in 'proxy', 'deploy', 'setup') {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $graph $group) -File -Filter '*.json') {
        $unit = (Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json).Data
        foreach ($candidate in @($unit.Source) + @($unit.Includes)) {
            if (-not $candidate) { continue }
            $fullPath = [IO.Path]::GetFullPath($candidate)
            if ($fullPath.StartsWith($checkoutPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                [void]$paths.Add($fullPath)
            }
        }
    }
}
$proxy = Join-Path $checkoutPrefix 'Tools\AOClientRoomSpaceGuard\ProxyDll'
foreach ($file in 'Build-Package.cmd', 'src\version_proxy.def', 'src\deploy_tool.manifest', 'src\setup_tool.manifest') {
    [void]$paths.Add((Join-Path $proxy $file))
}
$hashes = @{}
foreach ($file in $paths) {
    $relative = $file.Substring($checkoutPrefix.Length).Replace('\', '/')
    $stream = [IO.File]::OpenRead($file)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $hashes[$relative] = [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '')
    } finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}
$json = ConvertTo-Json -InputObject $hashes
[IO.File]::WriteAllText((Join-Path $graph 'source-hashes.json'), $json)
