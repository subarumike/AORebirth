param(
    [string]$RepoRoot = "C:\Users\Mike\Documents\Cellao-Clean",
    [string]$TeleportCsv = "C:\Users\Mike\Documents\Cellao-Clean\tools-temp\playfield-teleport-audit.csv",
    [string]$WallCsv = "C:\Users\Mike\Documents\Cellao-Clean\tools-temp\playfield-wall-exit-audit.csv",
    [string]$MarkdownOutput = "C:\Users\Mike\Documents\Cellao-Clean\docs\playfield-remap-candidates.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Add-AssemblyIfNeeded {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing assembly: $Path"
    }

    Add-Type -Path $Path
}

function Convert-ToUnsignedIdentityInstance {
    param($Value)

    $asInt64 = [int64]$Value
    if ($asInt64 -lt 0) {
        $asInt64 += 4294967296
    }

    return [uint32]$asInt64
}

function Format-IdentityHex {
    param($Value)
    return "0x{0:X8}" -f (Convert-ToUnsignedIdentityInstance $Value)
}

function Get-DoorRows {
    param([int]$PlayfieldId)

    if (-not [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.ContainsKey($PlayfieldId)) {
        return @()
    }

    $pf = [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData[$PlayfieldId]
    return @($pf.Statels | Where-Object { [int]$_.Identity.Type -eq 51016 } | ForEach-Object {
        [pscustomobject]@{
            Playfield = $PlayfieldId
            PlayfieldName = $pf.Name
            Instance = [uint32](Convert-ToUnsignedIdentityInstance $_.Identity.Instance)
            InstanceHex = Format-IdentityHex $_.Identity.Instance
            TemplateId = $_.TemplateId
            X = [double]$_.X
            Y = [double]$_.Y
            Z = [double]$_.Z
            Coords = "{0:0.###},{1:0.###},{2:0.###}" -f $_.X, $_.Y, $_.Z
        }
    })
}

function Get-HorizontalDistance {
    param($A, $B)

    $dx = [double]$A.X - [double]$B.X
    $dz = [double]$A.Z - [double]$B.Z
    return [Math]::Sqrt(($dx * $dx) + ($dz * $dz))
}

$built = Join-Path $RepoRoot "CellAO\Built\Debug"
$playfieldsDat = Join-Path $built "playfields.dat"

$previousCurrentDirectory = [Environment]::CurrentDirectory
[Environment]::CurrentDirectory = $built
Push-Location $built
try {
    Add-AssemblyIfNeeded (Join-Path $built "MsgPack.dll")
    Add-AssemblyIfNeeded (Join-Path $built "SmokeLounge.AOtomation.Messaging.dll")
    Add-AssemblyIfNeeded (Join-Path $built "CellAO.Enums.dll")
    Add-AssemblyIfNeeded (Join-Path $built "CellAO.Interfaces.dll")
    Add-AssemblyIfNeeded (Join-Path $built "CellAO.Core.dll")
    Add-AssemblyIfNeeded (Join-Path $built "PlayfieldLoader.dll")

    [ZoneEngine.Core.Playfields.PlayfieldLoader]::CacheAllPlayfieldData($playfieldsDat) | Out-Null
} finally {
    Pop-Location
    [Environment]::CurrentDirectory = $previousCurrentDirectory
}

$teleports = @(Import-Csv -LiteralPath $TeleportCsv)
$walls = @(Import-Csv -LiteralPath $WallCsv)

$knownCorrectDoorByPlayfield = @{
    1187 = "0xC00204A3"
    2064 = "0xC0010810"
    2073 = "0xC0010819"
    4704 = "0xC0001260"
}

$definiteTeleportProblems = @($teleports | Where-Object {
    $_.Warnings -match "missing destination|duplicate destination|missing source playfield|missing source statel"
})

$wallProblems = @($walls | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Warnings) })

$shopLike = $teleports | Where-Object {
    $_.DestinationPlayfieldName -match "shop|smarket|Supermarket|pharmacist|implant|armor|clothes|nano|weapon|book|store"
}

$interiorGroups = $shopLike | Group-Object DestinationPlayfield, DestinationPlayfieldName | Sort-Object Count -Descending
$doorPairCandidates = New-Object System.Collections.Generic.List[object]

foreach ($group in $interiorGroups) {
    $first = $group.Group | Select-Object -First 1
    $destinationPlayfield = [int]$first.DestinationPlayfield
    $doors = @(Get-DoorRows $destinationPlayfield)
    if ($doors.Count -lt 2) {
        continue
    }

    $usedInstances = @{}
    foreach ($row in $group.Group) {
        $usedInstances[[uint32]$row.DestinationInstanceDecimal] = $true
    }

    foreach ($usedDoor in $doors | Where-Object { $usedInstances.ContainsKey($_.Instance) }) {
        $nearest = @($doors | Where-Object {
            $_.Instance -ne $usedDoor.Instance -and $_.TemplateId -eq $usedDoor.TemplateId
        } | ForEach-Object {
            [pscustomobject]@{
                Door = $_
                Distance = Get-HorizontalDistance $usedDoor $_
            }
        } | Sort-Object Distance | Select-Object -First 1)

        if ($nearest.Count -eq 0) {
            continue
        }

        $candidate = $nearest[0]
        if ($candidate.Distance -gt 30.0) {
            continue
        }

        $confidence = "Medium"
        $reason = "same-template sibling door within 30m"
        if ($candidate.Distance -le 12.0) {
            $confidence = "High-review"
            $reason = "same-template paired door within 12m; matches known implant-shop failure pattern"
        }

        $inboundIds = ($group.Group | Where-Object { [uint32]$_.DestinationInstanceDecimal -eq $usedDoor.Instance } | Select-Object -ExpandProperty Id) -join ", "
        $doorPairCandidates.Add([pscustomobject]@{
            DestinationPlayfield = $destinationPlayfield
            DestinationPlayfieldName = $first.DestinationPlayfieldName
            InboundIds = $inboundIds
            CurrentDoor = $usedDoor.InstanceHex
            CurrentCoords = $usedDoor.Coords
            CandidateDoor = $candidate.Door.InstanceHex
            CandidateCoords = $candidate.Door.Coords
            TemplateId = $usedDoor.TemplateId
            Distance = "{0:0.###}" -f $candidate.Distance
            Confidence = $confidence
            Reason = $reason
        })
    }
}

$rankedCandidates = @($doorPairCandidates | ForEach-Object {
    $rank = "Needs one live confirmation"
    $nextAction = "Capture or test one representative before patching this family."

    if ($knownCorrectDoorByPlayfield.ContainsKey([int]$_.DestinationPlayfield)) {
        if ($knownCorrectDoorByPlayfield[[int]$_.DestinationPlayfield] -eq $_.CurrentDoor) {
            $rank = "Verified fixed / reference"
            $nextAction = "No patch. Keep as reference evidence for this interior family."
        } else {
            $rank = "Do not patch blindly"
            $nextAction = "This is the old side of an already-fixed door pair; verify source data before changing."
        }
    } elseif ($_.DestinationPlayfieldName -match "implants_shop" -and $_.Confidence -eq "High-review" -and $_.CurrentDoor -match "^0xC000") {
        $rank = "Auto-fix safe"
        $nextAction = "Patch this destination door to the paired sibling, then spot-test one advanced implant shop."
    } elseif ($_.DestinationPlayfieldName -match "smarket|Supermarket" -and $_.Confidence -eq "High-review") {
        $rank = "Needs one live confirmation"
        $nextAction = "Confirm one matching supermarket/Fair Trade entrance-exit pair, then patch the family."
    } elseif ($_.DestinationPlayfieldName -match "smarket|Supermarket") {
        $rank = "Needs family review"
        $nextAction = "Review as a supermarket-family candidate; distance is larger than the verified close-door pattern."
    } elseif ($_.Confidence -eq "High-review") {
        $rank = "Needs one live confirmation"
        $nextAction = "High-confidence geometry, but no verified family pattern yet."
    }

    $_ | Add-Member -NotePropertyName Rank -NotePropertyValue $rank -PassThru |
        Add-Member -NotePropertyName NextAction -NotePropertyValue $nextAction -PassThru
})

$autoFixSafe = @($rankedCandidates | Where-Object { $_.Rank -eq "Auto-fix safe" })
$needsLiveConfirmation = @($rankedCandidates | Where-Object { $_.Rank -eq "Needs one live confirmation" })
$needsFamilyReview = @($rankedCandidates | Where-Object { $_.Rank -eq "Needs family review" })
$verifiedFixed = @($rankedCandidates | Where-Object { $_.Rank -eq "Verified fixed / reference" })

$rankedCsv = Join-Path (Split-Path -Parent $MarkdownOutput) "..\tools-temp\playfield-remap-ranked-candidates.csv"
$rankedCandidates | Sort-Object Rank, DestinationPlayfieldName, CurrentDoor | Export-Csv -LiteralPath $rankedCsv -NoTypeInformation

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# Playfield Remap Candidates")
$markdown.Add("")
$markdown.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$markdown.Add("")
$markdown.Add("Source of truth used:")
$markdown.Add("- Current-client playfield data: $playfieldsDat")
$markdown.Add("- Teleport audit CSV: $TeleportCsv")
$markdown.Add("- Wall exit audit CSV: $WallCsv")
$markdown.Add("")
$markdown.Add("## Purpose")
$markdown.Add("")
$markdown.Add("This report identifies old CellAO mapping data that does not line up cleanly with the current AO client playfield data. It is a repair-planning report, not an auto-patch list.")
$markdown.Add("")
$markdown.Add("## Summary")
$markdown.Add("")
$markdown.Add("| Category | Count | Meaning |")
$markdown.Add("| --- | ---: | --- |")
$markdown.Add("| Definite teleport problems | $($definiteTeleportProblems.Count) | Missing source/destination playfield or statel identity in current playfields.dat |")
$markdown.Add("| Wall exit problems | $($wallProblems.Count) | Wall exit points at missing/null/invalid destination data |")
$markdown.Add("| Interior paired-door candidates | $($doorPairCandidates.Count) | Current target has a nearby same-template sibling door that may be the real exit/entry |")
$markdown.Add("| Auto-fix safe candidates | $($autoFixSafe.Count) | Candidate matches a verified family pattern closely enough for a focused patch + spot-test |")
$markdown.Add("| Needs one live confirmation | $($needsLiveConfirmation.Count) | Candidate has strong geometry but no direct verified family representative yet |")
$markdown.Add("| Needs family review | $($needsFamilyReview.Count) | Candidate is related to a known family but has weaker geometry than the verified pattern |")
$markdown.Add("| Verified fixed/reference candidates | $($verifiedFixed.Count) | Already-fixed pairs retained as evidence references |")
$markdown.Add("")

$markdown.Add("## Confirmed Pattern")
$markdown.Add("")
$markdown.Add("The confirmed failures so far were stale destination-door mappings:")
$markdown.Add("")
$markdown.Add("| Interior | Old Door | Corrected Door | Evidence |")
$markdown.Add("| --- | --- | --- | --- |")
$markdown.Add("| Neutral Supermarket Advanced / Superior-style interior `1187` | `C00004A3` | `C00204A3` | Old target was in/near main room; correct current-client exterior door is at `205,5,120` |")
$markdown.Add("| Neutral Basic Implant Shop `2064` | `C0000810` | `C0010810` | Old target was the inner-room doorway; real exterior exit is `C0010810` at `191,5,164` |")
$markdown.Add("| Neutral Advanced Implant Shop `2073` | `C0000819` | `C0010819` | Same paired-door pattern as `2064`; Borealis entrance playtested successfully after remap |")
$markdown.Add("| Tower Shop `4704` | `C0001260` | `C0001260` | Borealis tower shop entrance playtested successfully; nearby paired door is not a repair target |")
$markdown.Add("")

$markdown.Add("## Ranked Repair Plan")
$markdown.Add("")
$markdown.Add("This ranking is intentionally conservative. `Auto-fix safe` still means patch plus one spot-test, not a blind mass rewrite.")
$markdown.Add("")
$markdown.Add("### Auto-Fix Safe")
$markdown.Add("")
if ($autoFixSafe.Count -eq 0) {
    $markdown.Add("No auto-fix-safe candidates found.")
} else {
    $markdown.Add("| Destination | Inbound Teleport Ids | Current Target | Candidate Target | Distance | Next Action |")
    $markdown.Add("| --- | --- | --- | --- | ---: | --- |")
    foreach ($row in $autoFixSafe | Sort-Object DestinationPlayfieldName, CurrentDoor) {
        $markdown.Add("| $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) | $($row.InboundIds) | $($row.CurrentDoor) | $($row.CandidateDoor) | $($row.Distance) | $($row.NextAction) |")
    }
}
$markdown.Add("")

$markdown.Add("### Needs One Live Confirmation")
$markdown.Add("")
if ($needsLiveConfirmation.Count -eq 0) {
    $markdown.Add("No one-confirmation candidates found.")
} else {
    $markdown.Add("| Destination | Inbound Teleport Ids | Current Target | Candidate Target | Distance | Next Action |")
    $markdown.Add("| --- | --- | --- | --- | ---: | --- |")
    foreach ($row in $needsLiveConfirmation | Sort-Object DestinationPlayfieldName, CurrentDoor) {
        $markdown.Add("| $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) | $($row.InboundIds) | $($row.CurrentDoor) | $($row.CandidateDoor) | $($row.Distance) | $($row.NextAction) |")
    }
}
$markdown.Add("")

$markdown.Add("### Needs Family Review")
$markdown.Add("")
if ($needsFamilyReview.Count -eq 0) {
    $markdown.Add("No family-review candidates found.")
} else {
    $markdown.Add("| Destination | Inbound Teleport Ids | Current Target | Candidate Target | Distance | Next Action |")
    $markdown.Add("| --- | --- | --- | --- | ---: | --- |")
    foreach ($row in $needsFamilyReview | Sort-Object DestinationPlayfieldName, CurrentDoor) {
        $markdown.Add("| $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) | $($row.InboundIds) | $($row.CurrentDoor) | $($row.CandidateDoor) | $($row.Distance) | $($row.NextAction) |")
    }
}
$markdown.Add("")

$markdown.Add("### Verified Fixed / Reference")
$markdown.Add("")
if ($verifiedFixed.Count -eq 0) {
    $markdown.Add("No verified reference rows found.")
} else {
    $markdown.Add("| Destination | Inbound Teleport Ids | Correct Target | Paired Door | Distance | Next Action |")
    $markdown.Add("| --- | --- | --- | --- | ---: | --- |")
    foreach ($row in $verifiedFixed | Sort-Object DestinationPlayfieldName, CurrentDoor) {
        $markdown.Add("| $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) | $($row.InboundIds) | $($row.CurrentDoor) | $($row.CandidateDoor) | $($row.Distance) | $($row.NextAction) |")
    }
}
$markdown.Add("")

$markdown.Add("## Definite Teleport Problems")
$markdown.Add("")
if ($definiteTeleportProblems.Count -eq 0) {
    $markdown.Add("No definite teleport identity problems found.")
} else {
    $markdown.Add("| Id | Source | Source Statel | Destination | Destination Statel | Warnings |")
    $markdown.Add("| ---: | --- | --- | --- | --- | --- |")
    foreach ($row in $definiteTeleportProblems | Select-Object -First 80) {
        $markdown.Add("| $($row.Id) | $($row.SourcePlayfield) $($row.SourcePlayfieldName) | $($row.SourceInstanceHex) | $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) | $($row.DestinationInstanceHex) | $($row.Warnings) |")
    }
    if ($definiteTeleportProblems.Count -gt 80) {
        $markdown.Add("")
        $markdown.Add("First 80 rows shown. Use `playfield-teleport-audit.csv` for the full list.")
    }
}
$markdown.Add("")

$markdown.Add("## Wall Exit Problems")
$markdown.Add("")
if ($wallProblems.Count -eq 0) {
    $markdown.Add("No wall exit problems found.")
} else {
    $markdown.Add("| Playfield | Segment | Start | End | Destination | Warnings |")
    $markdown.Add("| --- | ---: | --- | --- | --- | --- |")
    foreach ($row in $wallProblems | Select-Object -First 80) {
        $markdown.Add("| $($row.Playfield) $($row.PlayfieldName) | $($row.Segment) | $($row.Start) | $($row.End) | $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) index $($row.DestinationIndex) | $($row.Warnings) |")
    }
    if ($wallProblems.Count -gt 80) {
        $markdown.Add("")
        $markdown.Add("First 80 rows shown. Use `playfield-wall-exit-audit.csv` for the full list.")
    }
}
$markdown.Add("")

$markdown.Add("## Interior Paired-Door Candidates")
$markdown.Add("")
$markdown.Add("These are not guaranteed bad. They are current-client door pairs that match the class of bug we just fixed: the mapped destination door has a nearby same-template sibling door in the same interior.")
$markdown.Add("")
if ($doorPairCandidates.Count -eq 0) {
    $markdown.Add("No paired-door candidates found.")
} else {
    $markdown.Add("| Confidence | Destination | Inbound Teleport Ids | Current Target | Current Coords | Candidate Sibling | Candidate Coords | Distance | Reason |")
    $markdown.Add("| --- | --- | --- | --- | --- | --- | --- | ---: | --- |")
    foreach ($row in $doorPairCandidates | Sort-Object Confidence, DestinationPlayfieldName, CurrentDoor | Select-Object -First 120) {
        $markdown.Add("| $($row.Confidence) | $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) | $($row.InboundIds) | $($row.CurrentDoor) | $($row.CurrentCoords) | $($row.CandidateDoor) | $($row.CandidateCoords) | $($row.Distance) | $($row.Reason) |")
    }
    if ($doorPairCandidates.Count -gt 120) {
        $markdown.Add("")
        $markdown.Add("First 120 rows shown.")
    }
}
$markdown.Add("")

$markdown.Add("## Repair Rules")
$markdown.Add("")
$markdown.Add('1. Patch definite missing destination statels only after checking whether a nearby same-template replacement exists in current `playfields.dat`.')
$markdown.Add('2. Patch repeated interior families as groups once one representative is confirmed in-game.')
$markdown.Add('3. Do not fix wall exits through `teleports.sql`; wall exits need `WallCollision`/playfield destination handling or separate remap logic.')
$markdown.Add('4. Every patch must update both `CellAO/Libraries/Source/CellAO.Database/SqlTables/teleports.sql` and `cellao_codex_clean.teleports`.')

$markdown | Set-Content -LiteralPath $MarkdownOutput -Encoding UTF8

"Wrote $MarkdownOutput"
