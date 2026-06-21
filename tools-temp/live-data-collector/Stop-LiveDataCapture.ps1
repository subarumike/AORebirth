param(
    [string]$CaptureDir = "",
    [switch]$NoDecode,
    [ValidateSet("All", "Loot", "Quest", "PacketsOnly")]
    [string]$Mode = "",
    [string]$RepoRoot = "",
    [switch]$DryRun,
    [switch]$AllowWrite,
    [switch]$AllowStopProcess
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
$activePath = Join-Path $collectorRoot "active_capture.json"

if ([string]::IsNullOrWhiteSpace($CaptureDir)) {
    if (-not (Test-Path -LiteralPath $activePath)) {
        Write-Host "Resolved stop-capture paths:"
        Write-Host "  RepoRoot=$RepoRoot"
        Write-Host "  CollectorRoot=$collectorRoot"
        Write-Host "  ActivePath=$activePath"
        Write-Host "  IntendedAction=stop active capture and update metadata"
        Write-Host "No active capture file found. Pass -CaptureDir if you want to stop or decode a specific folder."
        if ($DryRun -or -not ($AllowStopProcess -and $AllowWrite)) {
            return
        }
        throw "No active capture file found. Pass -CaptureDir if you want to decode a specific folder."
    }
    $active = Get-Content -Raw -LiteralPath $activePath | ConvertFrom-Json
    $CaptureDir = [string]$active.capture_dir
} else {
    $active = $null
}

$metaPath = Join-Path $CaptureDir "capture_meta.json"
$meta = if (Test-Path -LiteralPath $metaPath) { Get-Content -Raw -LiteralPath $metaPath | ConvertFrom-Json } else { $null }
$targetPid = if ($meta -and $meta.pid) { [int]$meta.pid } elseif ($active -and $active.pid) { [int]$active.pid } else { 0 }
$decodeMode = if ([string]::IsNullOrWhiteSpace($Mode)) {
    if ($meta -and $meta.mode) { [string]$meta.mode } else { "All" }
} else {
    $Mode
}

Write-Host "Resolved stop-capture paths:"
Write-Host "  RepoRoot=$RepoRoot"
Write-Host "  CollectorRoot=$collectorRoot"
Write-Host "  ActivePath=$activePath"
Write-Host "  CaptureDir=$CaptureDir"
Write-Host "  MetaPath=$metaPath"
Write-Host "  TargetPid=$targetPid"
Write-Host "  DecodeAfterStop=$(-not $NoDecode)"
Write-Host "  DecodeMode=$decodeMode"

if ($DryRun -or -not ($AllowStopProcess -and $AllowWrite)) {
    Write-Host "No process stopped and no files written. Pass -AllowStopProcess -AllowWrite to stop and update capture metadata."
    return
}

if ($targetPid -gt 0) {
    $proc = Get-Process -Id $targetPid -ErrorAction SilentlyContinue
    if ($proc) {
        Stop-Process -Id $targetPid -Force
        Start-Sleep -Seconds 1
    }
}

if ($meta) {
    $meta | Add-Member -NotePropertyName status -NotePropertyValue "stopped" -Force
    $meta | Add-Member -NotePropertyName stopped_utc -NotePropertyValue (Get-Date).ToUniversalTime().ToString("o") -Force
    $meta | ConvertTo-Json -Depth 8 | Out-File -LiteralPath $metaPath -Encoding utf8
}

if (Test-Path -LiteralPath $activePath) {
    $current = Get-Content -Raw -LiteralPath $activePath | ConvertFrom-Json
    if ([string]$current.capture_dir -eq [string]$CaptureDir) {
        Remove-Item -LiteralPath $activePath -Force
    }
}

if (-not $NoDecode) {
    & powershell -NoProfile -File (Join-Path $collectorRoot "Decode-LiveDataCapture.ps1") -CaptureDir $CaptureDir -Mode $decodeMode -RepoRoot $RepoRoot -AllowWrite
    if ($LASTEXITCODE -ne 0) { throw "Decode failed" }
}

"Stopped capture: $CaptureDir"
