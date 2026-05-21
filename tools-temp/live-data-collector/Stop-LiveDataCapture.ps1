param(
    [string]$CaptureDir = "",
    [switch]$NoDecode,
    [ValidateSet("All", "Loot", "Quest", "PacketsOnly")]
    [string]$Mode = "",
    [string]$RepoRoot = "C:\Users\Mike\Documents\Cellao-Clean"
)

$ErrorActionPreference = "Stop"

$collectorRoot = Join-Path $RepoRoot "tools-temp\live-data-collector"
$activePath = Join-Path $collectorRoot "active_capture.json"

if ([string]::IsNullOrWhiteSpace($CaptureDir)) {
    if (-not (Test-Path -LiteralPath $activePath)) {
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
    $decodeMode = if ([string]::IsNullOrWhiteSpace($Mode)) {
        if ($meta -and $meta.mode) { [string]$meta.mode } else { "All" }
    } else {
        $Mode
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $collectorRoot "Decode-LiveDataCapture.ps1") -CaptureDir $CaptureDir -Mode $decodeMode
    if ($LASTEXITCODE -ne 0) { throw "Decode failed" }
}

"Stopped capture: $CaptureDir"
