[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$OutputDir,
    [string]$ConfigPath,
    [string]$MysqlPath = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe',
    [switch]$SkipDatabaseItemNames
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$allowedDatabaseName = 'cellao_codex_clean'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $RepoRoot 'CellAO\Documentation\MobLootCoverage'
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $RepoRoot 'CellAO\Config\Config.xml'
}

$mobTemplateSqlRelativePath = 'CellAO\Libraries\Source\CellAO.Database\SqlTables\mobtemplate.sql'
$mobDropTableSqlRelativePath = 'CellAO\Libraries\Source\CellAO.Database\SqlTables\mobdroptable.sql'
$mobTemplateSqlPath = Join-Path $RepoRoot $mobTemplateSqlRelativePath
$mobDropTableSqlPath = Join-Path $RepoRoot $mobDropTableSqlRelativePath

function ConvertTo-SqlInsertValues {
    param(
        [string]$Line
    )

    $valuesIndex = $Line.IndexOf('VALUES', [System.StringComparison]::OrdinalIgnoreCase)
    if ($valuesIndex -lt 0) {
        throw "SQL insert line does not contain VALUES: $Line"
    }

    $start = $Line.IndexOf('(', $valuesIndex)
    $end = $Line.LastIndexOf(')')
    if (($start -lt 0) -or ($end -le $start)) {
        throw "SQL insert line has no values list: $Line"
    }

    $payload = $Line.Substring($start + 1, $end - $start - 1)
    $values = [System.Collections.Generic.List[string]]::new()
    $builder = [System.Text.StringBuilder]::new()
    $inQuote = $false
    $quote = [char]0

    for ($i = 0; $i -lt $payload.Length; $i++) {
        $ch = $payload[$i]

        if ($inQuote) {
            if (($ch -eq '\') -and (($i + 1) -lt $payload.Length)) {
                [void]$builder.Append($payload[$i + 1])
                $i++
                continue
            }

            if ($ch -eq $quote) {
                if ((($i + 1) -lt $payload.Length) -and ($payload[$i + 1] -eq $quote)) {
                    [void]$builder.Append($quote)
                    $i++
                    continue
                }

                $inQuote = $false
                continue
            }

            [void]$builder.Append($ch)
            continue
        }

        if (($ch -eq "'") -or ($ch -eq '"')) {
            $inQuote = $true
            $quote = $ch
            continue
        }

        if ($ch -eq ',') {
            $values.Add($builder.ToString().Trim())
            [void]$builder.Clear()
            continue
        }

        [void]$builder.Append($ch)
    }

    $values.Add($builder.ToString().Trim())
    return @($values)
}

function ConvertTo-IntValue {
    param(
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return 0
    }

    return [int]$Value.Trim()
}

function Split-LootField {
    param(
        [string]$Value,
        [string]$Separator
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return @()
    }

    return @(
        $Value -split [System.Text.RegularExpressions.Regex]::Escape($Separator) |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ -ne '' }
    )
}

function Read-MobTemplates {
    param(
        [string]$Path
    )

    $rows = [System.Collections.Generic.List[object]]::new()
    foreach ($line in Get-Content -Path $Path) {
        if (-not $line.StartsWith('INSERT INTO `mobtemplate`', [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $values = ConvertTo-SqlInsertValues $line
        if ($values.Count -lt 25) {
            throw "mobtemplate insert has $($values.Count) values; expected at least 25: $line"
        }

        $rows.Add([pscustomobject]@{
            Hash = $values[0]
            MinLvl = ConvertTo-IntValue $values[1]
            MaxLvl = ConvertTo-IntValue $values[2]
            Side = ConvertTo-IntValue $values[3]
            Name = $values[8]
            NPCFamily = ConvertTo-IntValue $values[10]
            Health = ConvertTo-IntValue $values[11]
            MonsterData = ConvertTo-IntValue $values[12]
            MonsterScale = ConvertTo-IntValue $values[13]
            MobMeshs = $values[20]
            AdditionalMeshs = $values[21]
            DropHashes = $values[22]
            DropSlots = $values[23]
            DropRates = $values[24]
        }) | Out-Null
    }

    return @($rows)
}

function Read-MobDropTable {
    param(
        [string]$Path
    )

    $rows = [System.Collections.Generic.List[object]]::new()
    foreach ($line in Get-Content -Path $Path) {
        if (-not $line.StartsWith('INSERT INTO `mobdroptable`', [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $values = ConvertTo-SqlInsertValues $line
        if ($values.Count -lt 6) {
            throw "mobdroptable insert has $($values.Count) values; expected 6: $line"
        }

        $rows.Add([pscustomobject]@{
            Hash = $values[0]
            LowId = ConvertTo-IntValue $values[1]
            HighId = ConvertTo-IntValue $values[2]
            MinQl = ConvertTo-IntValue $values[3]
            MaxQl = ConvertTo-IntValue $values[4]
            RangeCheck = ConvertTo-IntValue $values[5]
        }) | Out-Null
    }

    return @($rows)
}

function ConvertFrom-ConnectionString {
    param(
        [string]$ConnectionString
    )

    $parts = @{}
    foreach ($part in ($ConnectionString -split ';')) {
        if ($part -match '^\s*([^=]+)=(.*)$') {
            $parts[$matches[1].Trim().ToLowerInvariant()] = $matches[2].Trim()
        }
    }

    return $parts
}

function Get-ConnectionValue {
    param(
        [hashtable]$Parts,
        [string]$Name
    )

    $key = $Name.ToLowerInvariant()
    if ($Parts.ContainsKey($key)) {
        return [string]$Parts[$key]
    }

    return ''
}

function Get-DatabaseItemNames {
    param(
        [int[]]$ItemIds,
        [string]$ConfigPath,
        [string]$MysqlPath,
        [string]$AllowedDatabaseName,
        [bool]$Skip
    )

    $names = @{}

    if ($Skip) {
        return [pscustomobject]@{
            Names = $names
            Loaded = $false
            Source = 'Skipped'
        }
    }

    if (-not (Test-Path -LiteralPath $MysqlPath)) {
        Write-Warning "mysql.exe was not found at $MysqlPath. Item names will be blank."
        return [pscustomobject]@{
            Names = $names
            Loaded = $false
            Source = 'MissingMysqlExe'
        }
    }

    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        Write-Warning "Config file was not found at $ConfigPath. Item names will be blank."
        return [pscustomobject]@{
            Names = $names
            Loaded = $false
            Source = 'MissingConfig'
        }
    }

    [xml]$config = Get-Content -Path $ConfigPath
    $connection = [string]$config.Config.MysqlConnection
    $parts = ConvertFrom-ConnectionString $connection
    $database = Get-ConnectionValue $parts 'database'

    if ($database -ne $AllowedDatabaseName) {
        throw "Refusing to read database '$database'. This exporter is locked to '$AllowedDatabaseName'."
    }

    $server = Get-ConnectionValue $parts 'server'
    $user = Get-ConnectionValue $parts 'uid'
    $password = Get-ConnectionValue $parts 'pwd'
    $ids = @($ItemIds | Sort-Object -Unique)

    if ($ids.Count -eq 0) {
        return [pscustomobject]@{
            Names = $names
            Loaded = $true
            Source = $database
        }
    }

    $query = "SELECT Id, Name FROM itemnames WHERE Id IN ($($ids -join ','));"
    $mysqlArgs = @(
        '-h', $server,
        '-u', $user,
        "-p$password",
        "--database=$database",
        '--batch',
        '--raw',
        '--skip-column-names',
        '-e',
        $query
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $nativePreference = Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue
    if ($null -ne $nativePreference) {
        $previousNativePreference = $PSNativeCommandUseErrorActionPreference
        $PSNativeCommandUseErrorActionPreference = $false
    }

    try {
        $ErrorActionPreference = 'Continue'
        $output = & $MysqlPath @mysqlArgs 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not read item names from $database. Item names will be blank."
            return [pscustomobject]@{
                Names = $names
                Loaded = $false
                Source = 'QueryFailed'
            }
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        if ($null -ne $nativePreference) {
            $PSNativeCommandUseErrorActionPreference = $previousNativePreference
        }
    }

    foreach ($line in $output) {
        $columns = $line -split "`t", 2
        if ($columns.Count -ne 2) {
            continue
        }

        $itemId = 0
        if ([int]::TryParse($columns[0], [ref]$itemId)) {
            $names[$itemId] = $columns[1]
        }
    }

    return [pscustomobject]@{
        Names = $names
        Loaded = $true
        Source = $database
    }
}

function Get-ItemName {
    param(
        [hashtable]$ItemNames,
        [int]$ItemId
    )

    if ($ItemNames.ContainsKey($ItemId)) {
        return [string]$ItemNames[$ItemId]
    }

    return ''
}

function Get-ItemNameStatus {
    param(
        [hashtable]$ItemNames,
        [bool]$ItemNamesLoaded,
        [int]$LowId,
        [int]$HighId
    )

    if (-not $ItemNamesLoaded) {
        return 'ItemNamesNotLoaded'
    }

    $lowFound = $ItemNames.ContainsKey($LowId)
    $highFound = $ItemNames.ContainsKey($HighId)

    if ($LowId -eq $HighId) {
        if ($lowFound) {
            return 'ItemNameFound'
        }

        return 'ItemNameMissing'
    }

    if ($lowFound -and $highFound) {
        return 'RangeEndpointNamesFound'
    }

    return 'RangeEndpointNameMissing'
}

function ConvertTo-DropRatePercent {
    param(
        [string]$DropRateRaw
    )

    $dropRate = 0
    if ([int]::TryParse($DropRateRaw, [ref]$dropRate)) {
        return ('{0:0.##}' -f ($dropRate / 100.0))
    }

    return ''
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$mobTemplates = @(Read-MobTemplates $mobTemplateSqlPath)
$dropTableRows = @(Read-MobDropTable $mobDropTableSqlPath)
$itemIds = @(($dropTableRows | ForEach-Object { $_.LowId; $_.HighId }) | Sort-Object -Unique)
$itemNameResult = Get-DatabaseItemNames `
    -ItemIds $itemIds `
    -ConfigPath $ConfigPath `
    -MysqlPath $MysqlPath `
    -AllowedDatabaseName $allowedDatabaseName `
    -Skip ([bool]$SkipDatabaseItemNames)
$itemNames = [hashtable]$itemNameResult.Names
$itemNamesLoaded = [bool]$itemNameResult.Loaded

$dropRowsByHash = @{}
foreach ($row in $dropTableRows) {
    if (-not $dropRowsByHash.ContainsKey($row.Hash)) {
        $dropRowsByHash[$row.Hash] = [System.Collections.Generic.List[object]]::new()
    }

    $dropRowsByHash[$row.Hash].Add($row)
}

$dropTableItemRows = @(
    foreach ($row in $dropTableRows) {
        [pscustomobject]@{
            DropGroupHash = $row.Hash
            LowId = $row.LowId
            HighId = $row.HighId
            MinQl = $row.MinQl
            MaxQl = $row.MaxQl
            RangeCheck = $row.RangeCheck
            LowItemName = Get-ItemName $itemNames $row.LowId
            HighItemName = Get-ItemName $itemNames $row.HighId
            ItemNameStatus = Get-ItemNameStatus $itemNames $itemNamesLoaded $row.LowId $row.HighId
        }
    }
)

$referencedDropHashes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$missingDropHashes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$dropColumnCountMismatches = 0
$coverageRows = [System.Collections.Generic.List[object]]::new()

foreach ($template in $mobTemplates) {
    $dropHashExpressions = @(Split-LootField $template.DropHashes ',')
    $dropSlots = @(Split-LootField $template.DropSlots ',')
    $dropRates = @(Split-LootField $template.DropRates ',')
    $hasDrops = ($dropHashExpressions.Count -gt 0) -or ($dropSlots.Count -gt 0) -or ($dropRates.Count -gt 0)

    if (-not $hasDrops) {
        $coverageRows.Add([pscustomobject]@{
            TemplateHash = $template.Hash
            MobName = $template.Name
            MinLvl = $template.MinLvl
            MaxLvl = $template.MaxLvl
            NPCFamily = $template.NPCFamily
            MonsterData = $template.MonsterData
            DropSlot = ''
            DropRateRaw = ''
            DropRatePercent = ''
            DropGroupExpression = ''
            DropGroupHash = ''
            LowId = ''
            HighId = ''
            MinQl = ''
            MaxQl = ''
            RangeCheck = ''
            LowItemName = ''
            HighItemName = ''
            ValidationStatus = 'NoDropGroups'
        }) | Out-Null
        continue
    }

    $expectedCount = $dropHashExpressions.Count
    if (($dropSlots.Count -ne $expectedCount) -or ($dropRates.Count -ne $expectedCount)) {
        $dropColumnCountMismatches++
    }

    $slotCount = [Math]::Max($dropHashExpressions.Count, [Math]::Max($dropSlots.Count, $dropRates.Count))
    for ($i = 0; $i -lt $slotCount; $i++) {
        $expression = ''
        $slot = ''
        $rate = ''

        if ($i -lt $dropHashExpressions.Count) {
            $expression = $dropHashExpressions[$i]
        }

        if ($i -lt $dropSlots.Count) {
            $slot = $dropSlots[$i]
        }

        if ($i -lt $dropRates.Count) {
            $rate = $dropRates[$i]
        }

        $groupHashes = @(Split-LootField $expression '+')
        if ($groupHashes.Count -eq 0) {
            $coverageRows.Add([pscustomobject]@{
                TemplateHash = $template.Hash
                MobName = $template.Name
                MinLvl = $template.MinLvl
                MaxLvl = $template.MaxLvl
                NPCFamily = $template.NPCFamily
                MonsterData = $template.MonsterData
                DropSlot = $slot
                DropRateRaw = $rate
                DropRatePercent = ConvertTo-DropRatePercent $rate
                DropGroupExpression = $expression
                DropGroupHash = ''
                LowId = ''
                HighId = ''
                MinQl = ''
                MaxQl = ''
                RangeCheck = ''
                LowItemName = ''
                HighItemName = ''
                ValidationStatus = 'MissingDropExpression'
            }) | Out-Null
            continue
        }

        foreach ($groupHash in $groupHashes) {
            [void]$referencedDropHashes.Add($groupHash)

            if (-not $dropRowsByHash.ContainsKey($groupHash)) {
                [void]$missingDropHashes.Add($groupHash)
                $coverageRows.Add([pscustomobject]@{
                    TemplateHash = $template.Hash
                    MobName = $template.Name
                    MinLvl = $template.MinLvl
                    MaxLvl = $template.MaxLvl
                    NPCFamily = $template.NPCFamily
                    MonsterData = $template.MonsterData
                    DropSlot = $slot
                    DropRateRaw = $rate
                    DropRatePercent = ConvertTo-DropRatePercent $rate
                    DropGroupExpression = $expression
                    DropGroupHash = $groupHash
                    LowId = ''
                    HighId = ''
                    MinQl = ''
                    MaxQl = ''
                    RangeCheck = ''
                    LowItemName = ''
                    HighItemName = ''
                    ValidationStatus = 'MissingDropGroup'
                }) | Out-Null
                continue
            }

            foreach ($dropRow in $dropRowsByHash[$groupHash]) {
                $coverageRows.Add([pscustomobject]@{
                    TemplateHash = $template.Hash
                    MobName = $template.Name
                    MinLvl = $template.MinLvl
                    MaxLvl = $template.MaxLvl
                    NPCFamily = $template.NPCFamily
                    MonsterData = $template.MonsterData
                    DropSlot = $slot
                    DropRateRaw = $rate
                    DropRatePercent = ConvertTo-DropRatePercent $rate
                    DropGroupExpression = $expression
                    DropGroupHash = $groupHash
                    LowId = $dropRow.LowId
                    HighId = $dropRow.HighId
                    MinQl = $dropRow.MinQl
                    MaxQl = $dropRow.MaxQl
                    RangeCheck = $dropRow.RangeCheck
                    LowItemName = Get-ItemName $itemNames $dropRow.LowId
                    HighItemName = Get-ItemName $itemNames $dropRow.HighId
                    ValidationStatus = Get-ItemNameStatus $itemNames $itemNamesLoaded $dropRow.LowId $dropRow.HighId
                }) | Out-Null
            }
        }
    }
}

$distinctDropHashes = @($dropTableRows | Select-Object -ExpandProperty Hash -Unique | Sort-Object)
$unlinkedDropHashes = @($distinctDropHashes | Where-Object { -not $referencedDropHashes.Contains($_) })
$templatesWithDrops = @($mobTemplates | Where-Object {
    (-not [string]::IsNullOrWhiteSpace($_.DropHashes)) -or
    (-not [string]::IsNullOrWhiteSpace($_.DropSlots)) -or
    (-not [string]::IsNullOrWhiteSpace($_.DropRates))
})

$itemIdsMissingNames = ''
if ($itemNamesLoaded) {
    $itemIdsMissingNames = @($itemIds | Where-Object { -not $itemNames.ContainsKey($_) }).Count
}

$summaryRows = @(
    [pscustomobject]@{ Key = 'SourceMobTemplateSql'; Value = $mobTemplateSqlRelativePath }
    [pscustomobject]@{ Key = 'SourceMobDropTableSql'; Value = $mobDropTableSqlRelativePath }
    [pscustomobject]@{ Key = 'ItemNameSource'; Value = [string]$itemNameResult.Source }
    [pscustomobject]@{ Key = 'MobTemplates'; Value = $mobTemplates.Count }
    [pscustomobject]@{ Key = 'MobTemplatesWithConfiguredDrops'; Value = $templatesWithDrops.Count }
    [pscustomobject]@{ Key = 'MobTemplatesWithoutConfiguredDrops'; Value = ($mobTemplates.Count - $templatesWithDrops.Count) }
    [pscustomobject]@{ Key = 'DropTableRows'; Value = $dropTableRows.Count }
    [pscustomobject]@{ Key = 'DistinctDropHashes'; Value = $distinctDropHashes.Count }
    [pscustomobject]@{ Key = 'ReferencedDropHashes'; Value = $referencedDropHashes.Count }
    [pscustomobject]@{ Key = 'UnlinkedDropHashes'; Value = $unlinkedDropHashes.Count }
    [pscustomobject]@{ Key = 'MissingDropHashReferences'; Value = $missingDropHashes.Count }
    [pscustomobject]@{ Key = 'DropColumnCountMismatches'; Value = $dropColumnCountMismatches }
    [pscustomobject]@{ Key = 'ItemIdsMissingNames'; Value = $itemIdsMissingNames }
)

$coveragePath = Join-Path $OutputDir 'MobLootCoverage.csv'
$dropTableItemsPath = Join-Path $OutputDir 'MobDropTableItems.csv'
$summaryPath = Join-Path $OutputDir 'MobLootSummary.csv'

$coverageRows |
    Sort-Object TemplateHash, DropSlot, DropGroupHash, LowId |
    Export-Csv -Path $coveragePath -NoTypeInformation

$dropTableItemRows |
    Sort-Object DropGroupHash, LowId, HighId |
    Export-Csv -Path $dropTableItemsPath -NoTypeInformation

$summaryRows | Export-Csv -Path $summaryPath -NoTypeInformation

Write-Host "Wrote $coveragePath"
Write-Host "Wrote $dropTableItemsPath"
Write-Host "Wrote $summaryPath"
Write-Host "Mob templates: $($mobTemplates.Count)"
Write-Host "Templates with configured drops: $($templatesWithDrops.Count)"
Write-Host "Drop table rows: $($dropTableRows.Count)"
Write-Host "Distinct drop hashes: $($distinctDropHashes.Count)"
Write-Host "Unlinked drop hashes: $($unlinkedDropHashes.Count)"
