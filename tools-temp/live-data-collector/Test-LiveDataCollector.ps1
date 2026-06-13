param(
    [string]$RepoRoot = "",
    [string]$Python = "python",
    [string]$KnownCaptureDir = "",
    [string]$OfficialC2SOnlyCaptureDir = "",
    [switch]$SkipKnownCapture,
    [switch]$DryRun,
    [switch]$AllowWrite
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepoRoot {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

$RepoRoot = Resolve-RepoRoot $RepoRoot
if ([string]::IsNullOrWhiteSpace($KnownCaptureDir)) {
    $KnownCaptureDir = Join-Path $RepoRoot "tools-temp\live-pcaps\private-server-quest-batch\2026-05-10_23-44-53"
}
if ([string]::IsNullOrWhiteSpace($OfficialC2SOnlyCaptureDir)) {
    $OfficialC2SOnlyCaptureDir = Join-Path $RepoRoot "tools-temp\live-pcaps\live-official-weapon-equip\2026-05-24_22-09-21"
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

function Assert-File {
    param([string]$Path)
    Assert-True (Test-Path -LiteralPath $Path) "Expected file does not exist: $Path"
}

$collectorRoot = Join-Path $RepoRoot "tools-temp\live-data-collector"
$pythonScripts = @(
    (Join-Path $collectorRoot "Decode-LiveObservationCapture.py"),
    (Join-Path $collectorRoot "Decode-LiveS2CFrames.py"),
    (Join-Path $collectorRoot "Export-LivePacketCoverage.py"),
    (Join-Path $collectorRoot "Export-LiveCombatLootTimeline.py"),
    (Join-Path $RepoRoot "tools-temp\live-loot-observations\Export-LiveLootObservations.py"),
    (Join-Path $RepoRoot "tools-temp\live-quest-observations\Export-LiveQuestObservations.py")
)
$powerShellScripts = @(
    (Join-Path $collectorRoot "Start-LiveDataCapture.ps1"),
    (Join-Path $collectorRoot "Stop-LiveDataCapture.ps1"),
    (Join-Path $collectorRoot "Decode-LiveDataCapture.ps1"),
    (Join-Path $collectorRoot "Test-LiveDataCollector.ps1")
)

Write-Host "Resolved live data collector test paths:"
Write-Host "  RepoRoot=$RepoRoot"
Write-Host "  KnownCaptureDir=$KnownCaptureDir"
Write-Host "  OfficialC2SOnlyCaptureDir=$OfficialC2SOnlyCaptureDir"
Write-Host "  IntendedAction=compile scripts and optionally replay known captures"

if ($DryRun -or -not $AllowWrite) {
    foreach ($script in $pythonScripts + $powerShellScripts) {
        Assert-File $script
    }
    Write-Host "No py_compile, decode replay, or report writes performed. Pass -AllowWrite to run the mutating smoke test."
    return
}

foreach ($script in $pythonScripts) {
    Assert-File $script
    & $Python -m py_compile $script
    Assert-True ($LASTEXITCODE -eq 0) "Python compile failed: $script"
}

foreach ($script in $powerShellScripts) {
    Assert-File $script
    [scriptblock]::Create((Get-Content -Raw -LiteralPath $script)) | Out-Null
}

if (-not $SkipKnownCapture) {
    if (Test-Path -LiteralPath $KnownCaptureDir) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $collectorRoot "Decode-LiveDataCapture.ps1") -CaptureDir $KnownCaptureDir -Mode All -RepoRoot $RepoRoot -AllowWrite
        Assert-True ($LASTEXITCODE -eq 0) "Known capture decode failed"

        $questRows = @((Import-Csv -LiteralPath (Join-Path $KnownCaptureDir "quest_update_observations.csv")))
        $questRewardRows = @((Import-Csv -LiteralPath (Join-Path $KnownCaptureDir "quest_reward_events.csv")))
        $s2cRows = @((Import-Csv -LiteralPath (Join-Path $KnownCaptureDir "s2c_frames.csv")))
        $c2sRows = @((Import-Csv -LiteralPath (Join-Path $KnownCaptureDir "ao_frames.csv")))
        $coverageRows = @((Import-Csv -LiteralPath (Join-Path $KnownCaptureDir "live_packet_coverage.csv")))
        $timelineRows = @((Import-Csv -LiteralPath (Join-Path $KnownCaptureDir "live_combat_loot_timeline.csv")))
        $corpseSessionRows = @((Import-Csv -LiteralPath (Join-Path $KnownCaptureDir "live_corpse_sessions.csv")))

        Assert-True ($questRows.Count -ge 5) "Expected at least 5 quest rows from known capture"
        Assert-True ($questRewardRows.Count -ge 10) "Expected at least 10 quest reward/event rows from known capture"
        Assert-True ($null -ne ($questRewardRows | Where-Object { $_.event_kind -eq "mission_complete_chat" -and $_.text -match "Mission Complete" } | Select-Object -First 1)) "Expected mission completion chat in quest reward events"
        Assert-True ($null -ne ($questRewardRows | Where-Object { $_.event_kind -eq "feedback" -and $_.detail -match "message_id=0x" } | Select-Object -First 1)) "Expected decoded Feedback fields in quest reward events"
        Assert-True ($s2cRows.Count -ge 1000) "Expected at least 1000 S2C rows from known capture"
        Assert-True ($c2sRows.Count -ge 100) "Expected at least 100 C2S rows from known capture"
        Assert-True ($coverageRows.Count -ge 30) "Expected at least 30 packet coverage rows from known capture"
        Assert-True ($null -ne ($coverageRows | Where-Object { $_.capture_source -eq "private_server_199" -and $_.coverage_authority -eq "private_server_response_flow" } | Select-Object -First 1)) "Expected known private-server capture to be labeled as private-server response-flow evidence"
        Assert-True ($null -ne ($coverageRows | Where-Object { $_.packet_name -eq "AttackInfo" -and $_.status -eq "message_model_only" -and $_.message_model_file -match "AttackInfoMessage.cs" } | Select-Object -First 1)) "Expected AttackInfo coverage to find its AOtomation message model"
        Assert-True ($null -ne ($coverageRows | Where-Object { $_.packet_name -eq "CorpseFullUpdate" -and $_.status -in @("packet_builder", "handler_and_builder", "message_model_only") -and $_.packet_builder_file -match "CorpseFullUpdate.cs" -and $_.message_model_file -match "CorpseFullUpdateMessage.cs" } | Select-Object -First 1)) "Expected CorpseFullUpdate coverage to find its ZoneEngine packet builder and AOtomation message model"
        Assert-True ($timelineRows.Count -ge 300) "Expected at least 300 combat/loot timeline rows from known capture"
        Assert-True ($corpseSessionRows.Count -ge 1) "Expected at least 1 corpse session row from known capture"
        Assert-True ($null -ne ($timelineRows | Where-Object { $_.packet_name -eq "AttackInfo" -and $_.detail -match "damage=" } | Select-Object -First 1)) "Expected timeline AttackInfo rows to decode damage fields"
        Assert-True ($null -ne ($timelineRows | Where-Object { $_.packet_name -eq "HealthDamage" -and $_.detail -match "health_value=" } | Select-Object -First 1)) "Expected timeline HealthDamage rows to decode health fields"
        Assert-True ($null -eq ($timelineRows | Where-Object { $_.direction -eq "c2s" -and $_.packet_name -eq "Attack" -and $_.target -match "^195:" } | Select-Object -First 1)) "C2S Attack target decoding should not be shifted by the N3 base unknown byte"
        Assert-True ($null -ne ($timelineRows | Where-Object { $_.direction -eq "c2s" -and $_.packet_name -eq "ClientMoveItemToInventory" -and $_.detail -match "source_container=" -and $_.detail -match "target_placement=" } | Select-Object -First 1)) "Expected ClientMoveItemToInventory rows to decode source container and target placement"
        Assert-True ($null -eq ($timelineRows | Where-Object { $_.direction -eq "c2s" -and $_.packet_name -eq "ClientMoveItemToInventory" -and $_.target -eq "Backpack:00000000" } | Select-Object -First 1)) "ClientMoveItemToInventory source identities should not be compacted to Backpack:00000000"
    }
    else {
        Write-Warning "Known capture not found, skipping replay test: $KnownCaptureDir"
    }

    if (Test-Path -LiteralPath $OfficialC2SOnlyCaptureDir) {
        & $Python (Join-Path $collectorRoot "Export-LivePacketCoverage.py") $OfficialC2SOnlyCaptureDir --repo-root $RepoRoot --allow-write
        Assert-True ($LASTEXITCODE -eq 0) "Official C2S-only coverage export failed"
        $officialCoverageRows = @((Import-Csv -LiteralPath (Join-Path $OfficialC2SOnlyCaptureDir "live_packet_coverage.csv")))
        Assert-True ($null -ne ($officialCoverageRows | Where-Object { $_.capture_source -eq "official_live" -and $_.coverage_authority -eq "c2s_only_request_flow" } | Select-Object -First 1)) "Expected official weapon capture to be labeled as C2S-only request-flow evidence"
        Assert-True ($null -eq ($officialCoverageRows | Where-Object { [int]$_.s2c_count -gt 0 } | Select-Object -First 1)) "Official C2S-only capture should not claim decoded S2C coverage"
    }
}

"Live data collector smoke tests passed."
