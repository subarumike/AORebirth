Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'PacketView.cs'), (Join-Path $PSScriptRoot 'ProbeCapture.cs')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../.local/npc-inspect-probe'))
$testDir = Join-Path $root ('capture-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$checks = 0
function Check([bool]$condition, [string]$name) {
    if (!$condition) { throw "FAIL: $name" }
    $script:checks++
}
$prefix = Join-Path $testDir 'roundtrip'
$capture = New-Object NpcInspectProbe.ProbeCapture $prefix
$utc = [DateTime]::UtcNow
Check ($capture.Append($true,[byte[]]@(1,2,3),$utc,1.25)) 'outgoing saved'
Check ($capture.Append($false,[byte[]]@(4,5),$utc,2.5)) 'incoming saved'
$capture.Flush()
Check ($capture.Saved -eq 2 -and $capture.Bytes -eq 55) 'record accounting'
$capture.Dispose()
$capture.Dispose()
$reader = New-Object IO.BinaryReader ([IO.File]::OpenRead($prefix + '-packets.bin'))
try {
    Check ([Text.Encoding]::ASCII.GetString($reader.ReadBytes(8)) -eq 'NPIPCAP2') 'archive magic'
    Check ($reader.ReadByte() -eq 1) 'outgoing direction'
    Check ($reader.ReadInt64() -eq $utc.Ticks) 'UTC preserved'
    Check ($reader.ReadDouble() -eq 1.25) 'elapsed time preserved'
    Check ($reader.ReadInt32() -eq 3) 'payload length'
    Check ([BitConverter]::ToString($reader.ReadBytes(3)) -eq '01-02-03') 'exact payload'
    Check ($reader.ReadByte() -eq 0) 'incoming direction'
} finally { $reader.Dispose() }
$capture = New-Object NpcInspectProbe.ProbeCapture (Join-Path $testDir 'byte-cap')
try {
    $chunk = New-Object byte[] 1048576
    for ($i = 0; $i -lt 17; $i++) { [void]$capture.Append($false,$chunk,$utc,$i) }
    Check ($capture.Truncated -and $capture.Dropped -gt 0) 'byte cap explicit'
    Check ($capture.Bytes -le [NpcInspectProbe.ProbeCapture]::MaxBytes) 'byte cap bounded'
    $before = $capture.Bytes
    Check (!$capture.Append($false,[byte[]]@(1),$utc,20)) 'capture stays truncated'
    Check ($capture.Bytes -eq $before) 'truncated capture cannot grow'
} finally { $capture.Dispose() }
$capture = New-Object NpcInspectProbe.ProbeCapture (Join-Path $testDir 'packet-cap')
try {
    for ($i = 0; $i -lt 20001; $i++) { [void]$capture.Append($false,[byte[]]@(),$utc,$i) }
    Check ($capture.Saved -eq 20000 -and $capture.Dropped -eq 1 -and $capture.Truncated) 'packet cap bounded'
} finally { $capture.Dispose() }
Write-Host "CAPTURE_CHECKS=PASS ($checks checks; offline only). Synthetic artifacts: $testDir"
