param(
    [string]$RepoRoot = "C:\Users\Mike\Documents\Cellao-Clean"
)

$ErrorActionPreference = "Stop"

$collectorRoot = Join-Path $RepoRoot "tools-temp\live-data-collector"
$activePath = Join-Path $collectorRoot "active_capture.json"

if (Test-Path -LiteralPath $activePath) {
    $active = Get-Content -Raw -LiteralPath $activePath | ConvertFrom-Json
    $proc = Get-Process -Id $active.pid -ErrorAction SilentlyContinue
    if ($proc) {
        "ACTIVE pid=$($proc.Id) capture_dir=$($active.capture_dir)"
        return
    }

    "STALE active_capture.json points at exited pid=$($active.pid) capture_dir=$($active.capture_dir)"
    return
}

$latest = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "tools-temp\live-pcaps") -Recurse -Filter capture_meta.json -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($latest) {
    $meta = Get-Content -Raw -LiteralPath $latest.FullName | ConvertFrom-Json
    $captureDir = if ($meta.capture_dir) { [string]$meta.capture_dir } else { Split-Path -Parent $latest.FullName }
    $status = if ($meta.status) { [string]$meta.status } else { "unknown" }
    $mode = if ($meta.mode) { [string]$meta.mode } else { "unknown" }
    "INACTIVE latest_capture=$captureDir status=$status mode=$mode"
}
else {
    "INACTIVE no capture history found"
}
