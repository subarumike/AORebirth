param(
    [Parameter(Mandatory=$true)]
    [string]$CaptureDir,
    [ValidateSet("All", "Loot", "Quest", "PacketsOnly")]
    [string]$Mode = "All",
    [string]$Python = "python",
    [string]$RepoRoot = "",
    [string]$AoLoggerRoot = "C:\Users\Mike\Documents\AO Live Logger",
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

$collectorRoot = Join-Path $RepoRoot "tools-temp\live-data-collector"
$lootExporter = Join-Path $RepoRoot "tools-temp\live-loot-observations\Export-LiveLootObservations.py"
$questExporter = Join-Path $RepoRoot "tools-temp\live-quest-observations\Export-LiveQuestObservations.py"
$packetDecoder = Join-Path $collectorRoot "Decode-LiveObservationCapture.py"
$s2cDecoder = Join-Path $collectorRoot "Decode-LiveS2CFrames.py"
$coverageExporter = Join-Path $collectorRoot "Export-LivePacketCoverage.py"
$timelineExporter = Join-Path $collectorRoot "Export-LiveCombatLootTimeline.py"
$c2sDecoder = Join-Path $AoLoggerRoot "ao_capture_analyzer.py"

if (-not (Test-Path -LiteralPath $CaptureDir)) {
    throw "CaptureDir does not exist: $CaptureDir"
}

Write-Host "Resolved live data decode paths:"
Write-Host "  RepoRoot=$RepoRoot"
Write-Host "  CollectorRoot=$collectorRoot"
Write-Host "  CaptureDir=$CaptureDir"
Write-Host "  Mode=$Mode"
Write-Host "  PacketDecoder=$packetDecoder"
Write-Host "  S2CDecoder=$s2cDecoder"
Write-Host "  CoverageExporter=$coverageExporter"
Write-Host "  IntendedAction=decode capture and write derived reports"

if ($DryRun -or -not $AllowWrite) {
    Write-Host "No decode/export scripts run and no files written. Pass -AllowWrite to decode capture outputs."
    return
}

& $Python $packetDecoder $CaptureDir
if ($LASTEXITCODE -ne 0) { throw "Packet decode failed" }

if ($Mode -ne "PacketsOnly") {
    & $Python $c2sDecoder $CaptureDir
    if ($LASTEXITCODE -ne 0) { throw "C2S decode failed" }

    & $Python $s2cDecoder $CaptureDir --repo-root $RepoRoot --allow-write
    if ($LASTEXITCODE -ne 0) { throw "S2C decode failed" }
}

if ($Mode -in @("All", "Loot")) {
    & $Python $lootExporter $CaptureDir --repo-root $RepoRoot --client-root "C:\Funcom\Anarchy Online"
    if ($LASTEXITCODE -ne 0) { throw "Loot export failed" }
}

if ($Mode -in @("All", "Quest")) {
    & $Python $questExporter $CaptureDir
    if ($LASTEXITCODE -ne 0) { throw "Quest export failed" }
}

if ($Mode -ne "PacketsOnly") {
    & $Python $coverageExporter $CaptureDir --repo-root $RepoRoot --allow-write
    if ($LASTEXITCODE -ne 0) { throw "Packet coverage export failed" }

    & $Python $timelineExporter $CaptureDir
    if ($LASTEXITCODE -ne 0) { throw "Combat/loot timeline export failed" }
}

$packetCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "packets.csv") -ErrorAction SilentlyContinue)).Count
$c2sFrameCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "ao_frames.csv") -ErrorAction SilentlyContinue)).Count
$s2cFrameCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "s2c_frames.csv") -ErrorAction SilentlyContinue)).Count
$questRowCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "quest_update_observations.csv") -ErrorAction SilentlyContinue)).Count
$questRewardEventCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "quest_reward_events.csv") -ErrorAction SilentlyContinue)).Count
$lootBodyRowCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "loot_body_observations.csv") -ErrorAction SilentlyContinue)).Count
$lootDropRowCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "loot_drop_observations.csv") -ErrorAction SilentlyContinue)).Count
$packetCoverageRows = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "live_packet_coverage.csv") -ErrorAction SilentlyContinue))
$packetCoverageRowCount = $packetCoverageRows.Count
$packetCoverageMissingCount = @($packetCoverageRows | Where-Object { $_.status -eq "missing" }).Count
$packetCoverageModelOnlyCount = @($packetCoverageRows | Where-Object { $_.status -eq "message_model_only" }).Count
$packetCoverageSource = if ($packetCoverageRows.Count -gt 0) { [string]$packetCoverageRows[0].capture_source } else { "none" }
$packetCoverageAuthority = if ($packetCoverageRows.Count -gt 0) { [string]$packetCoverageRows[0].coverage_authority } else { "none" }
$combatLootTimelineRowCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "live_combat_loot_timeline.csv") -ErrorAction SilentlyContinue)).Count
$corpseSessionRowCount = @((Import-Csv -LiteralPath (Join-Path $CaptureDir "live_corpse_sessions.csv") -ErrorAction SilentlyContinue)).Count

$lines = @(
    "# Live Data Collection Summary",
    "",
    "- Capture: ``$CaptureDir``",
    "- Mode: ``$Mode``",
    "",
    "## Counts",
    "",
    "- Packets: $packetCount",
    "- C2S frames: $c2sFrameCount",
    "- S2C frames: $s2cFrameCount",
    "- Quest rows: $questRowCount",
    "- Quest reward events: $questRewardEventCount",
    "- Loot body rows: $lootBodyRowCount",
    "- Loot drop rows: $lootDropRowCount",
    "- Packet coverage rows: $packetCoverageRowCount",
    "- Packet coverage missing: $packetCoverageMissingCount",
    "- Packet coverage message-model-only: $packetCoverageModelOnlyCount",
    "- Packet coverage source: $packetCoverageSource",
    "- Packet coverage authority: $packetCoverageAuthority",
    "- Combat/loot timeline rows: $combatLootTimelineRowCount",
    "- Corpse session rows: $corpseSessionRowCount",
    "",
    "## Output Files",
    "- ``summary.md``",
    "- ``ao_combat_summary.md``",
    "- ``s2c_quest_decode_summary.md``",
    "- ``quest_reward_events.csv``",
    "- ``live_packet_coverage.md``",
    "- ``live_combat_loot_timeline.md``",
    "- ``live_corpse_sessions.csv``",
    "- ``live_loot_observation_export.md``",
    "- ``quest_batch_observation_export.md``"
)
$lines | Out-File -LiteralPath (Join-Path $CaptureDir "collection_summary.md") -Encoding utf8

$metaPath = Join-Path $CaptureDir "capture_meta.json"
$meta = if (Test-Path -LiteralPath $metaPath) {
    Get-Content -Raw -LiteralPath $metaPath | ConvertFrom-Json
} else {
    [pscustomobject]@{}
}
$meta | Add-Member -NotePropertyName capture_dir -NotePropertyValue $CaptureDir -Force
$meta | Add-Member -NotePropertyName mode -NotePropertyValue $Mode -Force
$meta | Add-Member -NotePropertyName status -NotePropertyValue "decoded" -Force
$meta | Add-Member -NotePropertyName decoded_utc -NotePropertyValue (Get-Date).ToUniversalTime().ToString("o") -Force
$meta | Add-Member -NotePropertyName decode_counts -NotePropertyValue ([pscustomobject]@{
    packets = $packetCount
    c2s_frames = $c2sFrameCount
    s2c_frames = $s2cFrameCount
    quest_rows = $questRowCount
    quest_reward_events = $questRewardEventCount
    loot_body_rows = $lootBodyRowCount
    loot_drop_rows = $lootDropRowCount
    packet_coverage_rows = $packetCoverageRowCount
    packet_coverage_missing = $packetCoverageMissingCount
    packet_coverage_message_model_only = $packetCoverageModelOnlyCount
    packet_coverage_source = $packetCoverageSource
    packet_coverage_authority = $packetCoverageAuthority
    combat_loot_timeline_rows = $combatLootTimelineRowCount
    corpse_session_rows = $corpseSessionRowCount
}) -Force
$meta | ConvertTo-Json -Depth 8 | Out-File -LiteralPath $metaPath -Encoding utf8

"Decoded capture: $CaptureDir"
