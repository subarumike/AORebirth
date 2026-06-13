param(
    [Parameter(Mandatory = $true)]
    [string]$PcapPath,

    [string]$OutDir = "",
    [string]$RepoRoot = "",
    [string]$TsharkPath = "C:\Program Files\Wireshark\tshark.exe",
    [string]$MessageDll = "",
    [switch]$DryRun,
    [switch]$AllowWrite
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepoRoot {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

$RepoRoot = Resolve-RepoRoot $RepoRoot

if (-not (Test-Path -LiteralPath $PcapPath)) {
    throw "Pcap not found: $PcapPath"
}

if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path (Split-Path -Parent $PcapPath) "decoded"
}

if ([string]::IsNullOrWhiteSpace($MessageDll)) {
    $MessageDll = Join-Path $RepoRoot "CellAO\Built\Debug\SmokeLounge.AOtomation.Messaging.dll"
}

$payloadTsv = Join-Path $OutDir "tcp-payloads.tsv"
$clientDecoded = Join-Path $OutDir "client-n3-decoded.txt"
$chatText = Join-Path $OutDir "chat-strings.txt"

Write-Host "Resolved PCAP decode paths:"
Write-Host "  RepoRoot=$RepoRoot"
Write-Host "  PcapPath=$PcapPath"
Write-Host "  OutDir=$OutDir"
Write-Host "  TsharkPath=$TsharkPath"
Write-Host "  MessageDll=$MessageDll"
Write-Host "  IntendedOutputs=$payloadTsv, $clientDecoded, $chatText"

if ($DryRun -or -not $AllowWrite) {
    Write-Host "No decode outputs written. Pass -AllowWrite to create decoded files."
    return
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

if (-not (Test-Path -LiteralPath $TsharkPath)) {
    throw "tshark not found: $TsharkPath"
}

if (-not (Test-Path -LiteralPath $MessageDll)) {
    throw "AOtomation messaging DLL not found: $MessageDll"
}

& $TsharkPath -r $PcapPath -T fields `
    -e frame.number `
    -e frame.time_relative `
    -e ip.src `
    -e tcp.srcport `
    -e ip.dst `
    -e tcp.dstport `
    -e tcp.len `
    -e tcp.stream `
    -e tcp.flags.fin `
    -e tcp.payload |
    Set-Content -LiteralPath $payloadTsv -Encoding ascii

$asm = [Reflection.Assembly]::LoadFrom($MessageDll)
$serializerType = $asm.GetType("SmokeLounge.AOtomation.Messaging.Serialization.MessageSerializer")
$serializer = [Activator]::CreateInstance($serializerType)

function Convert-HexToBytes([string]$hex) {
    if ([string]::IsNullOrWhiteSpace($hex)) {
        return [byte[]]@()
    }

    $bytes = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16)
    }

    return $bytes
}

function Format-Value($value) {
    if ($null -eq $value) {
        return "null"
    }

    if ($value -is [Array]) {
        $items = @()
        foreach ($item in $value) {
            $items += (Format-Value $item)
        }

        return "[" + ($items -join ",") + "]"
    }

    $type = $value.GetType()
    if ($type.FullName -like "SmokeLounge.AOtomation.Messaging.GameData.GameTuple*") {
        return "$(Format-Value $value.Value1)=$(Format-Value $value.Value2)"
    }

    $props = @($type.GetProperties() | Where-Object { $_.CanRead -and $_.GetIndexParameters().Count -eq 0 })
    if ($type.Namespace -like "SmokeLounge.AOtomation.Messaging.GameData" -and $props.Count -gt 0 -and $type.Name -ne "Identity") {
        return ($props | ForEach-Object { "$($_.Name)=$(Format-Value ($_.GetValue($value, $null)))" }) -join ";"
    }

    return $value.ToString()
}

$decodedLines = New-Object System.Collections.Generic.List[string]
$chatLines = New-Object System.Collections.Generic.List[string]

foreach ($line in Get-Content -LiteralPath $payloadTsv) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $parts = $line -split "`t"
    if ($parts.Count -lt 10) {
        continue
    }

    $frame = $parts[0]
    $time = $parts[1]
    $src = $parts[2]
    $sport = $parts[3]
    $dst = $parts[4]
    $dport = $parts[5]
    $tcpLen = [int]$parts[6]
    $stream = $parts[7]
    $hex = $parts[9]

    if ($tcpLen -le 0 -or [string]::IsNullOrWhiteSpace($hex)) {
        continue
    }

    $bytes = Convert-HexToBytes $hex

    if ($sport -eq "7105" -or $dport -eq "7105") {
        $ascii = [Text.Encoding]::ASCII.GetString($bytes)
        $printable = -join ($ascii.ToCharArray() | ForEach-Object {
            if ([int][char]$_ -ge 32 -and [int][char]$_ -le 126) { $_ } else { "." }
        })
        $chatLines.Add("frame=$frame t=$time stream=$stream $src`:$sport -> $dst`:$dport len=$tcpLen text=$printable")
    }

    $streamObject = New-Object IO.MemoryStream(,$bytes)
    try {
        $message = $serializer.Deserialize($streamObject)
    } catch {
        continue
    }

    if ($null -eq $message) {
        continue
    }

    $header = $message.Header
    $body = $message.Body
    $props = @($body.GetType().GetProperties() | Where-Object {
        $_.CanRead -and $_.GetIndexParameters().Count -eq 0 -and $_.Name -ne "PacketType"
    })
    $details = ($props | ForEach-Object { "$($_.Name)=$(Format-Value ($_.GetValue($body, $null)))" }) -join " "

    $decodedLines.Add(
        "frame=$frame t=$time stream=$stream $src`:$sport -> $dst`:$dport len=$tcpLen msgId=$($header.MessageId) type=$($header.PacketType) size=$($header.Size) sender=0x$($header.Sender.ToString("X8")) receiver=0x$($header.Receiver.ToString("X8")) body=$($body.GetType().Name) $details"
    )
}

$decodedLines | Set-Content -LiteralPath $clientDecoded -Encoding utf8
$chatLines | Set-Content -LiteralPath $chatText -Encoding utf8

"payloads=$payloadTsv"
"clientDecoded=$clientDecoded"
"chatText=$chatText"
