[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$CaptureFolder = '20260614-194454',
    [int[]]$PacketNumbers = @(2757, 5045, 5339, 5497)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([Environment]::Is64BitProcess) {
    $wowPowerShell = Join-Path $env:WINDIR 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
    if (Test-Path -LiteralPath $wowPowerShell) {
        $escapedScript = $PSCommandPath.Replace("'", "''")
        $escapedCaptureFolder = $CaptureFolder.Replace("'", "''")
        $packetLiteral = '@(' + ($PacketNumbers -join ',') + ')'
        $command = "& '$escapedScript' -CaptureFolder '$escapedCaptureFolder' -PacketNumbers $packetLiteral"

        if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
            $escapedRepoRoot = $RepoRoot.Replace("'", "''")
            $command += " -RepoRoot '$escapedRepoRoot'"
        }

        & $wowPowerShell -NoProfile -ExecutionPolicy Bypass -Command $command
        exit $LASTEXITCODE
    }

    throw 'AOSharp capture assemblies are x86; 32-bit PowerShell was not found.'
}

function Convert-HexToBytes {
    param([string]$Hex)

    if (($Hex.Length % 2) -ne 0) {
        throw "Odd-length hex payload: $($Hex.Length)"
    }

    $bytes = New-Object byte[] ($Hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16)
    }

    return $bytes
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.GetType().GetProperty($Name)
    if ($null -eq $property) {
        return $null
    }

    return $property.GetValue($Object, $null)
}

function Get-ArrayCount {
    param([object]$Value)

    if ($null -eq $Value) {
        return 0
    }

    return @($Value).Count
}

function Convert-QuestHtmlToObjective {
    param([string]$LongInfo)

    if ([string]::IsNullOrWhiteSpace($LongInfo)) {
        return $null
    }

    $match = [regex]::Match(
        $LongInfo,
        'Mission Objective:\s*<BR>(?<objective>.*?)(</font>|$)',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $match.Success) {
        return $null
    }

    $objective = $match.Groups['objective'].Value
    $objective = [regex]::Replace($objective, '<[^>]+>', '')
    return [System.Net.WebUtility]::HtmlDecode($objective).Trim()
}

function Convert-QuestHtmlToPlainText {
    param([string]$LongInfo)

    if ([string]::IsNullOrWhiteSpace($LongInfo)) {
        return $null
    }

    $text = $LongInfo -replace '(?i)<br\s*/?>', "`n"
    $text = [regex]::Replace($text, '<[^>]+>', '')
    $text = [System.Net.WebUtility]::HtmlDecode($text)
    $text = [regex]::Replace($text, "`r?`n\s*`r?`n", "`n`n")
    return $text.Trim()
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..')).Path
}
else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$captureBin = Join-Path $RepoRoot 'tools-temp\AOSharpLiveCapture\bin\Debug'
$packetLog = Join-Path $captureBin "captures\$CaptureFolder\packets.hex.log"
$aoSharpCommon = Join-Path $captureBin 'AOSharp.Common.dll'
$aoSharpCore = Join-Path $captureBin 'AOSharp.Core.dll'

if (-not (Test-Path -LiteralPath $packetLog)) {
    throw "Packet log not found: $packetLog"
}

Set-Location $captureBin
Add-Type -Path $aoSharpCommon
Add-Type -Path $aoSharpCore

$packetSet = @{}
foreach ($packetNumber in $PacketNumbers) {
    $packetSet[[int]$packetNumber] = $true
}

$lines = Get-Content -LiteralPath $packetLog
$decoded = @()

for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
    $line = $lines[$lineIndex]
    $lineMatch = [regex]::Match(
        $line,
        '^(?<timestamp>\S+)\s+(?<direction>IN|OUT)\s+#(?<packet>\d+)\s+len=(?<length>\d+)\s+n3=(?<n3>\S+)\s+hex=(?<hex>[0-9A-F]+)$')

    if (-not $lineMatch.Success) {
        continue
    }

    $packetNumber = [int]$lineMatch.Groups['packet'].Value
    $n3 = $lineMatch.Groups['n3'].Value

    if ($n3 -ne 'QuestFullUpdate' -or -not $packetSet.ContainsKey($packetNumber)) {
        continue
    }

    $bytes = Convert-HexToBytes $lineMatch.Groups['hex'].Value
    $message = [AOSharp.Core.PacketFactory]::Disassemble($bytes)
    if ($null -eq $message -or $null -eq $message.Body) {
        throw "Could not deserialize packet #$packetNumber"
    }

    $body = $message.Body
    $quests = @(Get-PropertyValue $body 'Quests')
    foreach ($quest in $quests) {
        $longInfo = [string](Get-PropertyValue $quest 'LongInfo')
        $questActions = @(Get-PropertyValue $quest 'QuestActions')
        $questActionSummaries = @()
        foreach ($action in $questActions) {
            $questActionSummaries += [pscustomobject]@{
                Version = Get-PropertyValue $action 'Version'
                Action = [string](Get-PropertyValue $action 'Action')
                PlayfieldId = [string](Get-PropertyValue $action 'PlayfieldId')
                Position = [string](Get-PropertyValue $action 'Position')
            }
        }

        $decoded += [pscustomobject]@{
            CaptureFolder = $CaptureFolder
            PacketLog = "tools-temp/AOSharpLiveCapture/bin/Debug/captures/$CaptureFolder/packets.hex.log"
            PacketLogLine = $lineIndex + 1
            Timestamp = $lineMatch.Groups['timestamp'].Value
            Direction = $lineMatch.Groups['direction'].Value
            PacketNumber = $packetNumber
            Length = [int]$lineMatch.Groups['length'].Value
            QuestId = [string](Get-PropertyValue $quest 'QuestId')
            Unknown1 = Get-PropertyValue $quest 'Unknown1'
            Unknown2 = Get-PropertyValue $quest 'Unknown2'
            Unknown3 = Get-PropertyValue $quest 'Unknown3'
            Unknown4 = Get-PropertyValue $quest 'Unknown4'
            ShortInfo = [string](Get-PropertyValue $quest 'ShortInfo')
            MissionObjective = Convert-QuestHtmlToObjective $longInfo
            PlainText = Convert-QuestHtmlToPlainText $longInfo
            LinkedIdentityUnknownId1 = [string](Get-PropertyValue $quest 'UnknownId1')
            Unknown5 = Get-PropertyValue $quest 'Unknown5'
            Unknown6 = Get-PropertyValue $quest 'Unknown6'
            Unknown7 = Get-PropertyValue $quest 'Unknown7'
            Unknown8 = Get-PropertyValue $quest 'Unknown8'
            Unknown9 = Get-PropertyValue $quest 'Unknown9'
            Unknown10 = Get-PropertyValue $quest 'Unknown10'
            MissionItemDataCount = Get-ArrayCount (Get-PropertyValue $quest 'MissionItemData')
            UnknownId2 = [string](Get-PropertyValue $quest 'UnknownId2')
            MissionIconId = Get-PropertyValue $quest 'MissionIconId'
            Unknown20 = Get-PropertyValue $quest 'Unknown20'
            Unknown21 = Get-PropertyValue $quest 'Unknown21'
            QuestActionCount = Get-ArrayCount $questActions
            QuestActions = $questActionSummaries
            PlayerIdsCount = Get-ArrayCount (Get-PropertyValue $quest 'PlayerIds')
            CharacterInfoCount = Get-ArrayCount (Get-PropertyValue $quest 'CharacterInfos')
            QuestIdentityCount = Get-ArrayCount (Get-PropertyValue $quest 'QuestIdentities')
            FactionInfoCount = Get-ArrayCount (Get-PropertyValue $quest 'FactionInfos')
            Unknown28 = Get-PropertyValue $quest 'Unknown28'
        }
    }
}

if ($decoded.Count -eq 0) {
    throw "No QuestFullUpdate packets matched packet numbers: $($PacketNumbers -join ', ')"
}

$decoded | ConvertTo-Json -Depth 8
