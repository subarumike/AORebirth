Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'PacketView.cs')
$checks = 0
function Check([bool]$condition, [string]$name) {
    if (!$condition) { throw "FAIL: $name" }
    $script:checks++
}
function Set-U32([byte[]]$bytes, [int]$offset, [uint32]$value) {
    $bytes[$offset] = ($value -shr 24) -band 255
    $bytes[$offset+1] = ($value -shr 16) -band 255
    $bytes[$offset+2] = ($value -shr 8) -band 255
    $bytes[$offset+3] = $value -band 255
}
function Frame([int]$length, [uint32]$key) {
    $bytes = New-Object byte[] $length
    $bytes[3] = 10
    $bytes[6] = ($length -shr 8) -band 255
    $bytes[7] = $length -band 255
    Set-U32 $bytes 16 $key
    return ,$bytes
}
$reply = Frame 41 0x5A585F65
Set-U32 $reply 29 50000
Set-U32 $reply 33 123
Check ([NpcInspectProbe.PacketView]::IsInspectReply($reply, 50000, 123)) 'reply target matched'
Check (![NpcInspectProbe.PacketView]::IsInspectReply($reply, 50000, 124)) 'wrong target rejected'
Check (![NpcInspectProbe.PacketView]::IsInspectReply($reply, 51000, 123)) 'wrong type rejected'
Check (![NpcInspectProbe.PacketView]::IsInspectReply($null, 50000, 123)) 'null rejected'
for ($i = 0; $i -lt $reply.Length; $i++) {
    $short = New-Object byte[] $i
    [Array]::Copy($reply, $short, $i)
    Check (![NpcInspectProbe.PacketView]::IsInspectReply($short, 50000, 123)) "truncation $i rejected"
}
$reply[3] = 11
Check (![NpcInspectProbe.PacketView]::IsInspectReply($reply, 50000, 123)) 'wrong family rejected'
$reply[3] = 10
$reply[16] = 0
Check (![NpcInspectProbe.PacketView]::IsInspectReply($reply, 50000, 123)) 'wrong key rejected'
$request = Frame 55 0x5E477770
Set-U32 $request 20 50000
Set-U32 $request 24 42
Set-U32 $request 29 261
Set-U32 $request 37 50000
Set-U32 $request 41 123
Check ([NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 123)) 'request matched'
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 43, 50000, 123)) 'wrong actor rejected'
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 124)) 'wrong request target rejected'
$request[54] = 1
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 123)) 'invalid string length rejected'
$request[54] = 0
Set-U32 $request 29 260
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 123)) 'non-inspect action rejected'
Set-U32 $request 29 261
$request[6] = 0
$request[7] = 0
Check ([NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 123)) 'REGRESSION native pre-framing Inspect recognized'
Check (![NpcInspectProbe.PacketView]::IsN3($request, 0x5E477770, 55)) 'zero length still invalid for incoming framing'
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 43, 50000, 123)) 'pre-framing wrong actor rejected'
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 124)) 'pre-framing wrong target rejected'
for ($i = 0; $i -lt $request.Length; $i++) {
    $short = New-Object byte[] $i
    [Array]::Copy($request, $short, $i)
    Check (![NpcInspectProbe.PacketView]::IsInspectRequest($short, 50000, 42, 50000, 123)) "pre-framing truncation $i rejected"
}
$request[5] = 1
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 123)) 'partial framed header rejected'
$request[5] = 0
$request[7] = 54
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 123)) 'nonzero size mismatch rejected'
$request[7] = 0
$request[54] = 1
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 123)) 'pre-framing string size mismatch rejected'
$request[54] = 0
Set-U32 $request 29 260
Check (![NpcInspectProbe.PacketView]::IsInspectRequest($request, 50000, 42, 50000, 123)) 'pre-framing other action rejected'
$request[3] = 11
Check (![NpcInspectProbe.PacketView]::IsOutgoingN3($request, 0x5E477770, 55)) 'pre-framing wrong family rejected'
Check (![NpcInspectProbe.PacketView]::IsOutgoingN3($null, 0x5E477770, 55)) 'null outgoing rejected'
$high = Frame 37 0x5A585F65
Set-U32 $high 29 50000
Set-U32 $high 33 ([uint32]::MaxValue)
Check ([NpcInspectProbe.PacketView]::IsInspectReply($high, 50000, -1)) 'identity bits preserved'
$feedback = Frame 45 0x50544D19
Set-U32 $feedback 29 1234
Set-U32 $feedback 33 110
Set-U32 $feedback 37 0x030C85E4
[uint32]$unknown = 0
[uint32]$category = 0
[uint32]$message = 0
Check ([NpcInspectProbe.PacketView]::TryFeedback($feedback,[ref]$unknown,[ref]$category,[ref]$message)) 'feedback with extra bytes recognized'
Check ($unknown -eq 1234 -and $category -eq 110 -and $message -eq 0x030C85E4) 'feedback fields decoded'
Check ([NpcInspectProbe.PacketView]::IsInspectRejection($category,$message)) 'rejection lookup matched'
Check (![NpcInspectProbe.PacketView]::IsInspectRejection(111,$message)) 'wrong rejection category rejected'
Check (![NpcInspectProbe.PacketView]::IsInspectRejection(110,1234)) 'other feedback not rejection'
$feedback[7] = 0
Check (![NpcInspectProbe.PacketView]::TryFeedback($feedback,[ref]$unknown,[ref]$category,[ref]$message)) 'unframed incoming feedback rejected'
Check ($unknown -eq 0 -and $category -eq 0 -and $message -eq 0) 'failed decode clears fields'
Check (![NpcInspectProbe.PacketView]::TryFeedback($null,[ref]$unknown,[ref]$category,[ref]$message)) 'null feedback rejected'
Check ([NpcInspectProbe.PacketView]::Describe([byte[]]@(1,2)) -eq 'header=SHORT') 'short packet diagnostic safe'
Write-Host "PACKET_VIEW_CHECKS=PASS ($checks checks; synthetic framing only)"
