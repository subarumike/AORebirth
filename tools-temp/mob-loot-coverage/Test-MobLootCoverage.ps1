[CmdletBinding()]
param(
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-SummaryValue {
    param(
        [object[]]$Summary,
        [string]$Key
    )

    $row = $Summary | Where-Object { $_.Key -eq $Key } | Select-Object -First 1
    Assert-True ($null -ne $row) "Missing summary key '$Key'."
    return [string]$row.Value
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("cellao-mob-loot-coverage-" + [System.Guid]::NewGuid().ToString('N'))
$exporter = Join-Path $PSScriptRoot 'Export-MobLootCoverage.ps1'

try {
    & $exporter -RepoRoot $RepoRoot -OutputDir $tempRoot -SkipDatabaseItemNames | Out-Host

    $summaryPath = Join-Path $tempRoot 'MobLootSummary.csv'
    $coveragePath = Join-Path $tempRoot 'MobLootCoverage.csv'
    $dropItemsPath = Join-Path $tempRoot 'MobDropTableItems.csv'

    Assert-True (Test-Path -LiteralPath $summaryPath) 'Summary CSV was not created.'
    Assert-True (Test-Path -LiteralPath $coveragePath) 'Coverage CSV was not created.'
    Assert-True (Test-Path -LiteralPath $dropItemsPath) 'Drop table item CSV was not created.'

    $summary = @(Import-Csv -Path $summaryPath)
    Assert-True ((Get-SummaryValue $summary 'MobTemplates') -eq '168') 'Expected 168 mob templates in the current seed data.'
    Assert-True ((Get-SummaryValue $summary 'MobTemplatesWithConfiguredDrops') -eq '0') 'Expected no mob templates with configured drops yet.'
    Assert-True ((Get-SummaryValue $summary 'MobTemplatesWithoutConfiguredDrops') -eq '168') 'Expected all mob templates to be missing configured drops.'
    Assert-True ((Get-SummaryValue $summary 'DropTableRows') -eq '25') 'Expected 25 mobdroptable seed rows.'
    Assert-True ((Get-SummaryValue $summary 'DistinctDropHashes') -eq '15') 'Expected 15 distinct mobdroptable hashes.'
    Assert-True ((Get-SummaryValue $summary 'ReferencedDropHashes') -eq '0') 'Expected no referenced drop hashes yet.'
    Assert-True ((Get-SummaryValue $summary 'UnlinkedDropHashes') -eq '15') 'Expected all drop hashes to be unlinked.'
    Assert-True ((Get-SummaryValue $summary 'MissingDropHashReferences') -eq '0') 'Expected no missing references because there are no configured references.'
    Assert-True ((Get-SummaryValue $summary 'DropColumnCountMismatches') -eq '0') 'Expected no drop column mismatches.'

    $coverage = @(Import-Csv -Path $coveragePath)
    $dropItems = @(Import-Csv -Path $dropItemsPath)

    Assert-True ($coverage.Count -eq 168) 'Expected one no-drop coverage row per mob template.'
    Assert-True (@($coverage | Where-Object { $_.ValidationStatus -ne 'NoDropGroups' }).Count -eq 0) 'Expected all mob templates to report NoDropGroups.'
    Assert-True ($dropItems.Count -eq 25) 'Expected one drop-table item row per mobdroptable seed row.'
    Assert-True (@($dropItems | Where-Object { $_.ItemNameStatus -ne 'ItemNamesNotLoaded' }).Count -eq 0) 'Skipped DB item-name mode should mark every item name as not loaded.'

    Write-Host '[PASS] Mob loot coverage verification passed.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $tempFullPath = [System.IO.Path]::GetFullPath($tempRoot)
        $systemTempPath = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $tempFullPath.StartsWith($systemTempPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove temp path outside the system temp directory: $tempFullPath"
        }

        Remove-Item -LiteralPath $tempFullPath -Recurse -Force
    }
}
