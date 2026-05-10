param(
    [string]$AoClientPath = 'C:\Funcom\Anarchy Online',
    [string]$AodbBinPath = 'C:\Users\Mike\Documents\AO programs\aodb-master\AODB\bin\Debug',
    [string]$ZoneOutputPath,
    [string]$NpcOutputPath,
    [string]$JsonOutputPath,
    [int]$MaxHintStringsPerZone = 80
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if ([string]::IsNullOrWhiteSpace($ZoneOutputPath)) {
    $ZoneOutputPath = Join-Path $repoRoot 'CellAO\Documentation\ClientRdbZoneEnemyHints.csv'
}

if ([string]::IsNullOrWhiteSpace($NpcOutputPath)) {
    $NpcOutputPath = Join-Path $repoRoot 'CellAO\Documentation\ClientRdbNpcTemplateHints.csv'
}

$aodbCommon = Join-Path $AodbBinPath 'AODB.Common.dll'
$aodb = Join-Path $AodbBinPath 'AODB.dll'

if (-not (Test-Path $aodbCommon)) {
    throw "AODB.Common.dll was not found at $aodbCommon"
}

if (-not (Test-Path $aodb)) {
    throw "AODB.dll was not found at $aodb"
}

$idxPath = Join-Path $AoClientPath 'cd_image\data\db\ResourceDatabase.idx'
if (-not (Test-Path $idxPath)) {
    throw "AO ResourceDatabase.idx was not found at $idxPath"
}

function Add-AssemblyIfTypeMissing {
    param(
        [string]$TypeName,
        [string]$AssemblyPath
    )

    foreach ($assembly in [System.AppDomain]::CurrentDomain.GetAssemblies()) {
        if ($assembly.GetType($TypeName, $false) -ne $null) {
            return
        }
    }

    Add-Type -Path $AssemblyPath
}

function Select-UniquePreserveOrder {
    param([string[]]$Items)

    $seen = @{}
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($item in $Items) {
        if ([string]::IsNullOrWhiteSpace($item)) {
            continue
        }

        $trimmed = $item.Trim()
        $key = $trimmed.ToLowerInvariant()
        if (-not $seen.ContainsKey($key)) {
            $seen[$key] = $true
            $result.Add($trimmed)
        }
    }

    return $result.ToArray()
}

function Get-PrintableStrings {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return @()
    }

    return [regex]::Matches($Text, '[ -~]{4,}') |
        ForEach-Object { $_.Value.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

function Get-RawText {
    param(
        [object]$Controller,
        [int]$RecordType,
        [int]$RecordId
    )

    $raw = $Controller.GetRaw($RecordType, $RecordId)
    if ($null -eq $raw) {
        return $null
    }

    return $script:TextEncoding.GetString($raw)
}

function Get-MatchingHints {
    param([string]$Text)

    $strings = Get-PrintableStrings $Text
    return Select-UniquePreserveOrder @($strings | Where-Object { $_ -match $script:EnemyMarkerRegex })
}

function Get-EnemyKeywords {
    param([string[]]$Hints)

    $keywords = New-Object System.Collections.Generic.List[string]
    foreach ($hint in $Hints) {
        foreach ($match in [regex]::Matches($hint, $script:EnemyMarkerRegex)) {
            $value = $match.Value.ToLowerInvariant()
            switch -Regex ($value) {
                '^rhinomen$' { $value = 'rhinoman'; break }
                '^[fsjb]?mob$' { $value = 'mob'; break }
                '^npcs?$' { $value = 'npc'; break }
            }

            $keywords.Add($value)
        }
    }

    return Select-UniquePreserveOrder $keywords.ToArray()
}

function Select-RdbDisplayName {
    param([string[]]$Strings)

    $clean = @($Strings | Where-Object { $_ -match '[A-Za-z]' })
    if ($clean.Count -eq 0) {
        return ''
    }

    $enemyName = $clean |
        Where-Object { $_.Length -gt 2 -and $_ -match $script:EnemyMarkerRegex } |
        Select-Object -First 1
    if (-not [string]::IsNullOrWhiteSpace($enemyName)) {
        return $enemyName
    }

    $plainName = $clean |
        Where-Object { $_.Length -gt 2 -and $_ -match '^[A-Za-z0-9][A-Za-z0-9 ''._:/()\-]+$' } |
        Select-Object -First 1
    if (-not [string]::IsNullOrWhiteSpace($plainName)) {
        return $plainName
    }

    return ($clean | Sort-Object Length -Descending | Select-Object -First 1)
}

Add-AssemblyIfTypeMissing 'AODB.Common.RDBObjects.RDBObject' $aodbCommon
Add-AssemblyIfTypeMissing 'AODB.RdbController' $aodb

$script:TextEncoding = [System.Text.Encoding]::GetEncoding(1252)
$script:EnemyMarkerRegex = '(?i)(leet|rhinoman|rhinomen|rollerrat|snake|flea|bronto|lizard|mantis|cyborg|mutant|biofreak|hound|scorpiod|buzzsaw|minibull|salamander|anun|shade|reaper|spider|mechdog|vulture|reptile|malle|disease|creature|monster|dynacamp|\b[FSJB]?MOB\b|\bNPCS?\b)'

$zoneSourceTypes = @(
    @{ Type = 1000014; Name = 'District' },
    @{ Type = 1000029; Name = 'Area' },
    @{ Type = 1000026; Name = 'Statel' }
)

$controller = New-Object AODB.RdbController($AoClientPath)
try {
    $recordMap = $controller.RecordTypeToId
    if (-not $recordMap.ContainsKey(1000001)) {
        throw 'RDB playfield record type 1000001 was not found.'
    }

    $zoneRows = New-Object System.Collections.Generic.List[object]
    foreach ($playfieldId in ($recordMap[1000001].Keys | Sort-Object)) {
        $playfieldText = Get-RawText $controller 1000001 $playfieldId
        if ($null -eq $playfieldText) {
            continue
        }

        $playfieldName = (Get-PrintableStrings $playfieldText | Select-Object -First 1)
        if ([string]::IsNullOrWhiteSpace($playfieldName)) {
            $playfieldName = "Playfield $playfieldId"
        }

        $sourceHits = @{}
        $allHints = New-Object System.Collections.Generic.List[string]
        $sourceNames = New-Object System.Collections.Generic.List[string]

        foreach ($source in $zoneSourceTypes) {
            $sourceText = Get-RawText $controller $source.Type $playfieldId
            $hints = Get-MatchingHints $sourceText
            if ($hints.Count -gt 0) {
                $sourceHits[$source.Name] = $hints
                $sourceNames.Add("$($source.Name):$($source.Type)")
                foreach ($hint in $hints) {
                    $allHints.Add($hint)
                }
            }
            else {
                $sourceHits[$source.Name] = @()
            }
        }

        if ($allHints.Count -eq 0) {
            continue
        }

        $uniqueHints = Select-UniquePreserveOrder $allHints.ToArray()
        $limitedHints = $uniqueHints | Select-Object -First $MaxHintStringsPerZone
        $keywords = Get-EnemyKeywords $uniqueHints

        $zoneRows.Add(
            [pscustomobject]@{
                PlayfieldId = $playfieldId
                PlayfieldName = $playfieldName
                EnemyKeywords = ($keywords -join '; ')
                HintStrings = ($limitedHints -join ' | ')
                DistrictHints = ((Select-UniquePreserveOrder $sourceHits['District']) -join ' | ')
                AreaHints = ((Select-UniquePreserveOrder $sourceHits['Area']) -join ' | ')
                StatelHints = ((Select-UniquePreserveOrder $sourceHits['Statel']) -join ' | ')
                SourceRecordTypes = (($sourceNames.ToArray() | Select-Object -Unique) -join '; ')
            })
    }

    $npcRows = New-Object System.Collections.Generic.List[object]
    if ($recordMap.ContainsKey(1040023)) {
        foreach ($templateId in ($recordMap[1040023].Keys | Sort-Object)) {
            $text = Get-RawText $controller 1040023 $templateId
            $strings = Select-UniquePreserveOrder (Get-PrintableStrings $text)
            if ($strings.Count -eq 0) {
                continue
            }

            $name = Select-RdbDisplayName $strings
            $keywords = Get-EnemyKeywords $strings
            $npcRows.Add(
                [pscustomobject]@{
                    TemplateId = $templateId
                    Name = $name
                    EnemyKeywords = ($keywords -join '; ')
                    Strings = (($strings | Select-Object -First 12) -join ' | ')
                })
        }
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $ZoneOutputPath -Parent) | Out-Null
    New-Item -ItemType Directory -Force -Path (Split-Path $NpcOutputPath -Parent) | Out-Null

    $zoneRows | Sort-Object PlayfieldId | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $ZoneOutputPath
    $npcRows | Sort-Object TemplateId | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $NpcOutputPath

    if (-not [string]::IsNullOrWhiteSpace($JsonOutputPath)) {
        New-Item -ItemType Directory -Force -Path (Split-Path $JsonOutputPath -Parent) | Out-Null
        [pscustomobject]@{
            GeneratedAtUtc = [DateTime]::UtcNow.ToString('o')
            AoClientPath = $AoClientPath
            ZoneHints = $zoneRows
            NpcTemplateHints = $npcRows
        } | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -Path $JsonOutputPath
    }

    Write-Host "Wrote $($zoneRows.Count) zone enemy hint rows to $ZoneOutputPath"
    Write-Host "Wrote $($npcRows.Count) NPC template hint rows to $NpcOutputPath"
}
finally {
    if ($controller -ne $null) {
        $controller.Dispose()
    }
}
