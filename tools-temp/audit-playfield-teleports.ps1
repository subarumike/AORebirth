param(
    [string]$RepoRoot = "C:\Users\Mike\Documents\Cellao-Clean",
    [string]$Database = "cellao_codex_clean",
    [string]$MysqlExe = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
    [string]$MysqlUser = "cellaodbuser",
    [string]$MysqlPassword = "1Guiness4828!",
    [string]$MarkdownOutput = "C:\Users\Mike\Documents\Cellao-Clean\docs\playfield-teleport-audit.md",
    [string]$TeleportCsvOutput = "C:\Users\Mike\Documents\Cellao-Clean\tools-temp\playfield-teleport-audit.csv",
    [string]$WallCsvOutput = "C:\Users\Mike\Documents\Cellao-Clean\tools-temp\playfield-wall-exit-audit.csv"
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

function Format-CoordList {
    param($Statels)

    if ($null -eq $Statels -or $Statels.Count -eq 0) {
        return ""
    }

    return ($Statels | ForEach-Object {
        "{0:0.###},{1:0.###},{2:0.###}" -f $_.X, $_.Y, $_.Z
    }) -join "; "
}

function Convert-ToUnsignedIdentityInstance {
    param($Value)

    $asInt64 = [int64]$Value
    if ($asInt64 -lt 0) {
        $asInt64 += 4294967296
    }

    return [uint32]$asInt64
}

function Get-StatelsByIdentity {
    param(
        [int]$PlayfieldId,
        [int]$Type,
        [uint32]$Instance
    )

    if (-not [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.ContainsKey($PlayfieldId)) {
        return @()
    }

    $pf = [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData[$PlayfieldId]
    return @($pf.Statels | Where-Object {
        [int]$_.Identity.Type -eq $Type -and (Convert-ToUnsignedIdentityInstance $_.Identity.Instance) -eq $Instance
    })
}

function Get-PlayfieldName {
    param([int]$PlayfieldId)

    if (-not [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.ContainsKey($PlayfieldId)) {
        return ""
    }

    return [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData[$PlayfieldId].Name
}

function Get-Warnings {
    param(
        [int]$SourcePlayfieldExists,
        [int]$DestinationPlayfieldExists,
        $SourceStatels,
        $DestinationStatels
    )

    $warnings = New-Object System.Collections.Generic.List[string]

    if ($SourcePlayfieldExists -eq 0) {
        $warnings.Add("missing source playfield")
    }

    if ($DestinationPlayfieldExists -eq 0) {
        $warnings.Add("missing destination playfield")
    }

    if ($SourceStatels.Count -eq 0) {
        $warnings.Add("missing source statel")
    } elseif ($SourceStatels.Count -gt 1) {
        $warnings.Add("duplicate source statel identity")
    }

    if ($DestinationStatels.Count -eq 0) {
        $warnings.Add("missing destination statel")
    } elseif ($DestinationStatels.Count -gt 1) {
        $warnings.Add("duplicate destination statel identity")
    }

    return ($warnings -join "; ")
}

$built = Join-Path $RepoRoot "CellAO\Built\Debug"
$playfieldsDat = Join-Path $built "playfields.dat"
if (-not (Test-Path -LiteralPath $playfieldsDat)) {
    throw "Missing playfields.dat. Build the project first: $playfieldsDat"
}

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

$teleportQuery = @"
SELECT Id,playfield,statelType,statelInstance,destinationPlayfield,destinationType,destinationInstance
FROM teleports
ORDER BY playfield,Id;
"@

$oldErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    $rawRows = & $MysqlExe "-u$MysqlUser" "-p$MysqlPassword" $Database --batch --raw --skip-column-names -e $teleportQuery 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "mysql exited with code $LASTEXITCODE"
    }
} finally {
    $ErrorActionPreference = $oldErrorActionPreference
}
$teleportRows = foreach ($line in $rawRows) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $parts = $line -split "`t"
    [pscustomobject]@{
        Id = [int]$parts[0]
        SourcePlayfield = [int]$parts[1]
        SourceType = [int]$parts[2]
        SourceInstance = [uint32]$parts[3]
        DestinationPlayfield = [int]$parts[4]
        DestinationType = [int]$parts[5]
        DestinationInstance = [uint32]$parts[6]
    }
}

$teleportAudit = foreach ($row in $teleportRows) {
    $sourcePfExists = [int][ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.ContainsKey($row.SourcePlayfield)
    $destPfExists = [int][ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.ContainsKey($row.DestinationPlayfield)
    $sourceStatels = @(Get-StatelsByIdentity $row.SourcePlayfield $row.SourceType $row.SourceInstance)
    $destStatels = @(Get-StatelsByIdentity $row.DestinationPlayfield $row.DestinationType $row.DestinationInstance)

    [pscustomobject]@{
        Id = $row.Id
        SourcePlayfield = $row.SourcePlayfield
        SourcePlayfieldName = Get-PlayfieldName $row.SourcePlayfield
        SourceType = $row.SourceType
        SourceInstanceDecimal = $row.SourceInstance
        SourceInstanceHex = ("0x{0:X8}" -f $row.SourceInstance)
        SourceStatelCount = $sourceStatels.Count
        SourceCoords = Format-CoordList $sourceStatels
        DestinationPlayfield = $row.DestinationPlayfield
        DestinationPlayfieldName = Get-PlayfieldName $row.DestinationPlayfield
        DestinationType = $row.DestinationType
        DestinationInstanceDecimal = $row.DestinationInstance
        DestinationInstanceHex = ("0x{0:X8}" -f $row.DestinationInstance)
        DestinationStatelCount = $destStatels.Count
        DestinationCoords = Format-CoordList $destStatels
        Warnings = Get-Warnings $sourcePfExists $destPfExists $sourceStatels $destStatels
    }
}

$teleportAudit | Export-Csv -LiteralPath $TeleportCsvOutput -NoTypeInformation

$wallAudit = foreach ($pfEntry in [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.GetEnumerator() | Sort-Object Key) {
    $pfId = [int]$pfEntry.Key
    $pfData = $pfEntry.Value

    for ($wallSetIndex = 0; $wallSetIndex -lt $pfData.Walls.Count; $wallSetIndex++) {
        $wallSet = $pfData.Walls[$wallSetIndex]
        for ($segmentIndex = 0; $segmentIndex -lt $wallSet.Walls.Count; $segmentIndex++) {
            $first = $wallSet.Walls[$segmentIndex]
            $second = $wallSet.Walls[($segmentIndex + 1) % $wallSet.Walls.Count]

            if ($second.DestinationPlayfield -le 0) {
                continue
            }

            $destSummary = ""
            $warnings = New-Object System.Collections.Generic.List[string]
            if (-not [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.ContainsKey($second.DestinationPlayfield)) {
                $warnings.Add("missing destination playfield")
            } else {
                $destPf = [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData[$second.DestinationPlayfield]
                if ($second.DestinationIndex -lt 0 -or $second.DestinationIndex -ge $destPf.Destinations.Count) {
                    $warnings.Add("invalid destination index")
                } else {
                    $dest = $destPf.Destinations[$second.DestinationIndex]
                    if ($null -eq $dest) {
                        $warnings.Add("null destination entry")
                    } else {
                        $destSummary = ($dest.ToString() -replace "`r?`n", " ")
                    }
                }
            }

            [pscustomobject]@{
                Playfield = $pfId
                PlayfieldName = $pfData.Name
                WallSet = $wallSetIndex
                Segment = $segmentIndex
                Start = "{0:0.###},{1:0.###}" -f $first.X, $first.Z
                End = "{0:0.###},{1:0.###}" -f $second.X, $second.Z
                DestinationPlayfield = $second.DestinationPlayfield
                DestinationPlayfieldName = Get-PlayfieldName $second.DestinationPlayfield
                DestinationIndex = $second.DestinationIndex
                Destination = $destSummary
                Warnings = ($warnings -join "; ")
            }
        }
    }
}

$wallAudit | Export-Csv -LiteralPath $WallCsvOutput -NoTypeInformation

$problemTeleports = @($teleportAudit | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Warnings) })
$problemWalls = @($wallAudit | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Warnings) })
$newlandRows = @($teleportAudit | Where-Object { $_.SourcePlayfield -in @(560,565,566,567) -or $_.DestinationPlayfield -in @(560,565,566,567,1186,1187,1193,1137) })

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# Playfield Teleport Audit")
$markdown.Add("")
$markdown.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$markdown.Add("")
$markdown.Add("Inputs:")
$markdown.Add("- Database: $Database")
$markdown.Add("- Playfield data: $playfieldsDat")
$markdown.Add("- Teleport CSV: $TeleportCsvOutput")
$markdown.Add("- Wall exit CSV: $WallCsvOutput")
$markdown.Add("")
$markdown.Add("## Summary")
$markdown.Add("")
$markdown.Add("| Area | Count |")
$markdown.Add("| --- | ---: |")
$markdown.Add("| Teleport rows audited | $($teleportAudit.Count) |")
$markdown.Add("| Teleport rows with warnings | $($problemTeleports.Count) |")
$markdown.Add("| Wall exit segments audited | $($wallAudit.Count) |")
$markdown.Add("| Wall exit segments with warnings | $($problemWalls.Count) |")
$markdown.Add("")

$markdown.Add("## Teleport Warnings")
$markdown.Add("")
if ($problemTeleports.Count -eq 0) {
    $markdown.Add("No missing or duplicate statel identity warnings found.")
} else {
    $markdown.Add("| Id | Source | Source Statel | Source Coords | Destination | Destination Statel | Destination Coords | Warnings |")
    $markdown.Add("| ---: | --- | --- | --- | --- | --- | --- | --- |")
    foreach ($row in $problemTeleports | Select-Object -First 100) {
        $markdown.Add("| $($row.Id) | $($row.SourcePlayfield) $($row.SourcePlayfieldName) | $($row.SourceInstanceHex) | $($row.SourceCoords) | $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) | $($row.DestinationInstanceHex) | $($row.DestinationCoords) | $($row.Warnings) |")
    }
    if ($problemTeleports.Count -gt 100) {
        $markdown.Add("")
        $markdown.Add("First 100 warnings shown. Use the CSV for the full list.")
    }
}
$markdown.Add("")

$markdown.Add("## Newland / Shop-Related Rows")
$markdown.Add("")
$markdown.Add("| Id | Source | Source Statel | Source Coords | Destination | Destination Statel | Destination Coords | Warnings |")
$markdown.Add("| ---: | --- | --- | --- | --- | --- | --- | --- |")
foreach ($row in $newlandRows) {
    $markdown.Add("| $($row.Id) | $($row.SourcePlayfield) $($row.SourcePlayfieldName) | $($row.SourceInstanceHex) | $($row.SourceCoords) | $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) | $($row.DestinationInstanceHex) | $($row.DestinationCoords) | $($row.Warnings) |")
}
$markdown.Add("")

$markdown.Add("## Wall Exit Warnings")
$markdown.Add("")
if ($problemWalls.Count -eq 0) {
    $markdown.Add("No missing wall destination warnings found.")
} else {
    $markdown.Add("| Playfield | Segment | Start | End | Destination | Warnings |")
    $markdown.Add("| --- | ---: | --- | --- | --- | --- |")
    foreach ($row in $problemWalls | Select-Object -First 100) {
        $markdown.Add("| $($row.Playfield) $($row.PlayfieldName) | $($row.Segment) | $($row.Start) | $($row.End) | $($row.DestinationPlayfield) $($row.DestinationPlayfieldName) index $($row.DestinationIndex) | $($row.Warnings) |")
    }
    if ($problemWalls.Count -gt 100) {
        $markdown.Add("")
        $markdown.Add("First 100 warnings shown. Use the CSV for the full list.")
    }
}
$markdown.Add("")
$markdown.Add("## How To Use")
$markdown.Add("")
$markdown.Add("- Statel teleport bugs are repaired in `CellAO/Libraries/Source/CellAO.Database/SqlTables/teleports.sql` and `cellao_codex_clean.teleports`.")
$markdown.Add("- Wall collision bugs are not repaired through `teleports.sql`; inspect `playfields.dat` wall exits and `WallCollision` handling.")
$markdown.Add("- For any bad in-game zone, capture/log the exact trigger first: `Statel collision firing` means a statel mapping issue; `Wall collision zoning` means a wall exit issue.")

$markdown | Set-Content -LiteralPath $MarkdownOutput -Encoding UTF8

"Wrote $MarkdownOutput"
"Wrote $TeleportCsvOutput"
"Wrote $WallCsvOutput"
