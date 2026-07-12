param(
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$AoClientPath = 'C:\Funcom\Anarchy Online',
    [string]$AodbBinPath = 'C:\Users\Mike\Documents\AO programs\aodb-master\AODB\bin\Debug'
)
$ErrorActionPreference = 'Stop'
$common = Join-Path $AodbBinPath 'AODB.Common.dll'
$aodb = Join-Path $AodbBinPath 'AODB.dll'
$idx = Join-Path $AoClientPath 'cd_image\data\db\ResourceDatabase.idx'
foreach ($required in @($common, $aodb, $idx)) { if (-not (Test-Path -LiteralPath $required)) { throw "Required RDB input missing: $required" } }
Add-Type -Path $common
Add-Type -Path $aodb
$encoding = [Text.Encoding]::GetEncoding(1252)
$sha256 = [Security.Cryptography.SHA256]::Create()
$controller = New-Object AODB.RdbController($AoClientPath)
try {
    $types = New-Object Collections.Generic.List[object]
    $payloadTypes = @(1000001, 1000014, 1000026, 1000029, 1040023)
    foreach ($typeId in ($controller.RecordTypeToId.Keys | Sort-Object)) {
        $records = New-Object Collections.Generic.List[object]
        if ($payloadTypes -contains [int]$typeId) { foreach ($recordId in ($controller.RecordTypeToId[$typeId].Keys | Sort-Object)) {
            $raw = $controller.GetRaw([int]$typeId, [int]$recordId)
            $strings = @()
            if ($null -ne $raw) {
                $text = $encoding.GetString($raw)
                $strings = @([regex]::Matches($text, '[ -~]{3,}') | ForEach-Object { $_.Value.Trim() } | Where-Object { $_ } | Select-Object -Unique)
            }
            $hash = if ($null -eq $raw) { $null } else { ([BitConverter]::ToString($sha256.ComputeHash($raw))).Replace('-', '').ToLowerInvariant() }
            $records.Add([ordered]@{ id=[int]$recordId; size=if ($null -eq $raw) { 0 } else { $raw.Length }; sha256=$hash; strings=$strings })
        } }
        $types.Add([ordered]@{ type=[int]$typeId; count=$controller.RecordTypeToId[$typeId].Count; records=$records })
    }
    $payload = [ordered]@{ source=$idx; source_timestamp_utc=(Get-Item -LiteralPath $idx).LastWriteTimeUtc.ToString('o'); record_types=$types }
    $parent = Split-Path -Parent $OutputPath
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    $payload | ConvertTo-Json -Depth 7 -Compress | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}
finally { $sha256.Dispose(); $controller.Dispose() }
