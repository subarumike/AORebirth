param([Parameter(Mandatory = $true)][string]$GamecodePath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Offline PE inspection only. Never load or execute the native DLL.
$expectedHash = '0948301922D0DF738879C2C375962A0A5B0248C48D430C296B46480B97930BBE'
if ((Get-FileHash -LiteralPath $GamecodePath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Unreviewed Gamecode build; static verification refused.'
}
$data = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $GamecodePath).Path)
function U16([int]$offset) { [BitConverter]::ToUInt16($data, $offset) }
function U32([int]$offset) { [BitConverter]::ToUInt32($data, $offset) }
$pe = U32 0x3c
if ((U32 $pe) -ne 0x4550 -or (U16 ($pe + 4)) -ne 0x14c) { throw 'Expected x86 PE.' }
$optional = $pe + 24
if ((U16 $optional) -ne 0x10b) { throw 'Expected PE32.' }
$sectionTable = $optional + (U16 ($pe + 20))
$sectionCount = U16 ($pe + 6)
function Offset([uint32]$rva) {
    for ($s = 0; $s -lt $sectionCount; $s++) {
        $section = $sectionTable + 40 * $s
        $start = U32 ($section + 12)
        $size = U32 ($section + 16)
        if ($rva -ge $start -and ($rva - $start) -lt $size) {
            return [int]((U32 ($section + 20)) + $rva - $start)
        }
    }
    throw ('RVA has no raw section: {0:X8}' -f $rva)
}
function AsciiAt([uint32]$rva) {
    $start = Offset $rva
    $end = $start
    while ($data[$end] -ne 0) { $end++ }
    [Text.Encoding]::ASCII.GetString($data, $start, $end - $start)
}
function ExpectBytes([uint32]$rva, [string]$hex) {
    $offset = Offset $rva
    $count = $hex.Length / 2
    $actual = [BitConverter]::ToString($data, $offset, $count).Replace('-', '')
    if ($actual -ne $hex) { throw ('Instruction mismatch at RVA {0:X8}' -f $rva) }
}
$exports = Offset (U32 ($optional + 96))
$names = Offset (U32 ($exports + 32))
$ordinals = Offset (U32 ($exports + 36))
$functions = Offset (U32 ($exports + 28))
$exportFound = $false
for ($i = 0; $i -lt (U32 ($exports + 24)); $i++) {
    if ((AsciiAt (U32 ($names + 4 * $i))) -eq '?N3Msg_Inspect@n3EngineClientAnarchy_t@@QAEXABVIdentity_t@@@Z') {
        $entry = U32 ($functions + 4 * (U16 ($ordinals + 2 * $i)))
        if ($entry -ne 0x1dc58) { throw 'Unexpected Inspect export RVA.' }
        $exportFound = $true
    }
}
if (!$exportFound) { throw 'Inspect export not found.' }
# Verified disassembly: ECX this, one identity-reference argument, action 261,
# native message constructor, SendIIRToObservers call, callee pops four bytes.
ExpectBytes 0x1dc6a '8BF1'
ExpectBytes 0x1dc94 '6805010000'
ExpectBytes 0x1dc9a 'FF7508'
ExpectBytes 0x1dcad 'E88D480500'
ExpectBytes 0x1dccb 'FF156C3F1510'
ExpectBytes 0x1dcf6 'C20400'
# Constructor copies both 32-bit words of the selected target identity.
ExpectBytes 0x725a1 '8B450CC706000E16108B08894E208B4004894624'
$descriptor = Offset (U32 ($optional + 104))
$sendFound = $false
while ((U32 ($descriptor + 12)) -ne 0) {
    $module = AsciiAt (U32 ($descriptor + 12))
    $lookupRva = U32 $descriptor
    $iatRva = U32 ($descriptor + 16)
    if ($lookupRva -eq 0) { $lookupRva = $iatRva }
    $lookup = Offset $lookupRva
    for ($i = 0; (U32 ($lookup + 4 * $i)) -ne 0; $i++) {
        if (($iatRva + 4 * $i) -eq 0x153f6c) {
            $name = AsciiAt ((U32 ($lookup + 4 * $i)) + 2)
            if ($module -ne 'N3.dll' -or $name -ne '?SendIIRToObservers@n3Dynel_t@@QAEXAAVn3InfoItemRemote_t@@@Z') {
                throw "Unexpected native send import: $module $name"
            }
            $sendFound = $true
        }
    }
    $descriptor += 20
}
if (!$sendFound) { throw 'SendIIRToObservers import was not resolved.' }
Write-Host 'NATIVE_INSPECT_STATIC=PASS (exact hash, x86 export, calling convention, identity, action 261, native send)'
Write-Host 'No DLL was loaded or executed. Live request/reply behavior remains unverified.'
