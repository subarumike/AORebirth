param(
    [string]$Title = "",
    [int]$TargetPid = 0,
    [int]$TimeoutSeconds = 90,
    [string[]]$MobAliases = @("beachleet", "shoresnake", "duneflea", "surflizard", "reefsalamander"),
    [switch]$NoBuild,
    [switch]$NoInject,
    [switch]$AllowAnyTarget
)

$ErrorActionPreference = "Stop"

$repo = "C:\Users\Mike\Documents\Cellao-Clean"
$runner = Join-Path $repo "tools-temp\run-local-combat-loot-smoke.ps1"

if (-not (Test-Path $runner)) {
    throw "Combat loot smoke runner not found: $runner"
}

$expandedMobAliases = @(
    foreach ($mobAliasGroup in $MobAliases) {
        foreach ($mobAlias in ($mobAliasGroup -split ",")) {
            $trimmed = $mobAlias.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $trimmed
            }
        }
    }
)

$first = $true
foreach ($mobAlias in $expandedMobAliases) {
    if ([string]::IsNullOrWhiteSpace($mobAlias)) {
        continue
    }

    Write-Host "=== Combat loot smoke: $mobAlias ==="

    $arguments = @{
        MobAlias = $mobAlias
        TimeoutSeconds = $TimeoutSeconds
    }

    if (-not [string]::IsNullOrWhiteSpace($Title)) {
        $arguments.Title = $Title
    }

    if ($TargetPid -gt 0) {
        $arguments.TargetPid = $TargetPid
    }

    if ($AllowAnyTarget) {
        $arguments.AllowAnyTarget = $true
    }

    if ($NoBuild -or -not $first) {
        $arguments.NoBuild = $true
    }

    if ($NoInject -or -not $first) {
        $arguments.NoInject = $true
    }

    & $runner @arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $first = $false
    Start-Sleep -Seconds 5
}

Write-Host "Combat loot smoke suite passed for: $($expandedMobAliases -join ', ')"
