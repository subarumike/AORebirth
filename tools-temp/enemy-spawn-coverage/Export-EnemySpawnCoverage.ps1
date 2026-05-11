param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$ModelViewerCatMeshPath = 'C:\Users\Mike\Documents\AO Decompiler\AO-Model-Viewer\Assets\Resources\CatMeshToMonsterData.txt',
    [string]$MobTemplateSqlPath = '',
    [string]$ZoneHintsPath = '',
    [string]$VisualOutputPath = '',
    [string]$CoverageOutputPath = ''
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($MobTemplateSqlPath)) {
    $MobTemplateSqlPath = Join-Path $RepoRoot 'CellAO\Libraries\Source\CellAO.Database\SqlTables\mobtemplate.sql'
}

if ([string]::IsNullOrWhiteSpace($ZoneHintsPath)) {
    $ZoneHintsPath = Join-Path $RepoRoot 'CellAO\Documentation\ClientRdbZoneEnemyHints.csv'
}

if ([string]::IsNullOrWhiteSpace($VisualOutputPath)) {
    $VisualOutputPath = Join-Path $RepoRoot 'CellAO\Documentation\MonsterDataCorpseVisualHints.csv'
}

if ([string]::IsNullOrWhiteSpace($CoverageOutputPath)) {
    $CoverageOutputPath = Join-Path $RepoRoot 'CellAO\Documentation\ClientHintedEnemyCoverage.csv'
}

function Get-FamilyKeyword {
    param(
        [string]$Name
    )

    $familyPatterns = [ordered]@{
        leet = '(?i)(^|\W)(leet|sleet|cheerleet|masculeet)(\W|$)'
        reet = '(?i)(^|\W)(reet|pareet)(\W|$)'
        snake = '(?i)(^|\W)snake(\W|$)'
        rollerrat = '(?i)rollerrat'
        flea = '(?i)(^|\W)flea(\W|$)'
        lizard = '(?i)(^|\W)lizard(\W|$)'
        malle = '(?i)(^|\W)malle(\W|$)'
        salamander = '(?i)(^|\W)salamander(\W|$)'
        spider = '(?i)(^|\W)spider(\W|$)'
        rhinoman = '(?i)rhinoman|rhinomen'
        bronto = '(?i)(^|\W)bronto(\W|$)'
        hound = '(?i)(^|\W)hound(\W|$)'
        mechdog = '(?i)(^|\W)mechdog(\W|$)'
        anun = '(?i)(^|\W)anun(\W|$)'
        scorpiod = '(?i)(^|\W)scorpiod(\W|$)'
        mantis = '(?i)(^|\W)mantis|mantez'
        mutant = '(?i)(^|\W)mutant(\W|$)'
        biofreak = '(?i)(^|\W)biofreak(\W|$)'
        cyborg = '(?i)(^|\W)cyborg(\W|$)'
    }

    foreach ($pattern in $familyPatterns.GetEnumerator()) {
        if ($Name -match $pattern.Value) {
            return $pattern.Key
        }
    }

    return ''
}

function Import-CatMeshMap {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        throw "Model-viewer CatMesh map not found at $Path"
    }

    $raw = Get-Content -Raw $Path
    $json = $raw | ConvertFrom-Json
    $monsterDataToCatMesh = @{}

    foreach ($property in $json.PSObject.Properties) {
        $catMesh = [int]$property.Name
        foreach ($monsterData in @($property.Value)) {
            $monsterDataValue = [int]$monsterData
            if (-not $monsterDataToCatMesh.ContainsKey($monsterDataValue)) {
                $monsterDataToCatMesh[$monsterDataValue] = $catMesh
            }
        }
    }

    return $monsterDataToCatMesh
}

function Import-MobTemplates {
    param(
        [string]$Path,
        [hashtable]$MonsterDataToCatMesh
    )

    if (-not (Test-Path $Path)) {
        throw "mobtemplate.sql not found at $Path"
    }

    $rows = @()
    $regex = [regex]"VALUES \('(?<Hash>[^']+)',(?<MinLvl>\d+),(?<MaxLvl>\d+),(?<Side>\d+),(?<Fatness>\d+),(?<Breed>\d+),(?<Sex>\d+),(?<Race>\d+),'(?<Name>(?:''|[^'])*)',(?<Flags>\d+),(?<NpcFamily>\d+),(?<Health>\d+),(?<MonsterData>\d+),(?<MonsterScale>\d+),"

    foreach ($line in Get-Content $Path) {
        $match = $regex.Match($line)
        if (-not $match.Success) {
            continue
        }

        $name = $match.Groups['Name'].Value -replace "''", "'"
        $familyKeyword = Get-FamilyKeyword -Name $name
        if ([string]::IsNullOrWhiteSpace($familyKeyword)) {
            continue
        }

        $monsterData = [int]$match.Groups['MonsterData'].Value
        $catMesh = ''
        $visualStatus = 'MissingCatMesh'
        if ($MonsterDataToCatMesh.ContainsKey($monsterData)) {
            $catMesh = $MonsterDataToCatMesh[$monsterData]
            $visualStatus = 'Mapped'
        }

        $rows += [pscustomobject]@{
            FamilyKeyword = $familyKeyword
            TemplateHash = $match.Groups['Hash'].Value
            TemplateName = $name
            MinLvl = [int]$match.Groups['MinLvl'].Value
            MaxLvl = [int]$match.Groups['MaxLvl'].Value
            NpcFamily = [int]$match.Groups['NpcFamily'].Value
            Health = [int]$match.Groups['Health'].Value
            MonsterData = $monsterData
            MonsterScale = [int]$match.Groups['MonsterScale'].Value
            CatMesh = $catMesh
            VisualStatus = $visualStatus
        }
    }

    return @($rows)
}

$supportedTestMobs = [ordered]@{
    leet = 'beachleet'
    reet = 'islandreet'
    snake = 'shoresnake'
    rollerrat = 'rollerrat'
    flea = 'duneflea'
    lizard = 'surflizard'
    malle = 'cliffmalle'
    salamander = 'reefsalamander'
    spider = 'alienspider'
}

$genericKeywords = @('dynacamp', 'mob', 'monster', 'creature', 'npc')

$catMeshMap = Import-CatMeshMap -Path $ModelViewerCatMeshPath
$templateRows = Import-MobTemplates -Path $MobTemplateSqlPath -MonsterDataToCatMesh $catMeshMap
$visualRows = @($templateRows | Sort-Object FamilyKeyword, MinLvl, TemplateName, TemplateHash)
$visualRows | Export-Csv -NoTypeInformation -Encoding UTF8 $VisualOutputPath

$mappedFamilies = @{}
foreach ($row in $visualRows) {
    if ($row.VisualStatus -eq 'Mapped' -and -not $mappedFamilies.ContainsKey($row.FamilyKeyword)) {
        $mappedFamilies[$row.FamilyKeyword] = $true
    }
}

$coverageRows = @()
foreach ($zone in Import-Csv $ZoneHintsPath) {
    $keywords = @($zone.EnemyKeywords -split ';' | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    $supported = @()
    $supportedKeys = @()
    $unsupported = @()
    $candidateUnsupported = @()
    $missing = @()
    $ignored = @()
    $ignoredWeakEvidence = @()
    $hasSpawnEvidence = $zone.SourceRecordTypes -match 'District:1000014|Area:1000029'

    foreach ($keyword in $keywords) {
        if ($genericKeywords -contains $keyword) {
            $ignored += $keyword
            continue
        }

        if (-not $hasSpawnEvidence) {
            $ignoredWeakEvidence += $keyword
            continue
        }

        if ($supportedTestMobs.Contains($keyword)) {
            $supported += $keyword
            $supportedKeys += $supportedTestMobs[$keyword]
            continue
        }

        $unsupported += $keyword
        if ($mappedFamilies.ContainsKey($keyword)) {
            $candidateUnsupported += $keyword
        }
        else {
            $missing += $keyword
        }
    }

    $coverageRows += [pscustomobject]@{
        PlayfieldId = $zone.PlayfieldId
        PlayfieldName = $zone.PlayfieldName
        SupportedEnemyKeywords = (($supported | Select-Object -Unique) -join '; ')
        SupportedTestMobKeys = (($supportedKeys | Select-Object -Unique) -join '; ')
        UnsupportedEnemyKeywords = (($unsupported | Select-Object -Unique) -join '; ')
        UnsupportedWithMappedTemplateCandidates = (($candidateUnsupported | Select-Object -Unique) -join '; ')
        MissingTemplateOrVisualKeywords = (($missing | Select-Object -Unique) -join '; ')
        IgnoredWeakEvidenceKeywords = (($ignoredWeakEvidence | Select-Object -Unique) -join '; ')
        IgnoredGenericKeywords = (($ignored | Select-Object -Unique) -join '; ')
    }
}

$coverageRows | Export-Csv -NoTypeInformation -Encoding UTF8 $CoverageOutputPath

Write-Host "Wrote $($visualRows.Count) visual/template candidate rows to $VisualOutputPath"
Write-Host "Wrote $($coverageRows.Count) playfield coverage rows to $CoverageOutputPath"
