param(
    [string]$ServerHost = "199.241.136.157",
    [string]$Interface = "4",
    [string]$Label = "private-server-auto",
    [ValidateSet("All", "Loot", "Quest", "PacketsOnly")]
    [string]$Mode = "All",
    [string]$RepoRoot = "C:\Users\Mike\Documents\Cellao-Clean",
    [string]$TsharkPath = "C:\Program Files\Wireshark\tshark.exe"
)

$ErrorActionPreference = "Stop"

$collectorRoot = Join-Path $RepoRoot "tools-temp\live-data-collector"
$activePath = Join-Path $collectorRoot "active_capture.json"
if (Test-Path -LiteralPath $activePath) {
    $active = Get-Content -Raw -LiteralPath $activePath | ConvertFrom-Json
    $existing = Get-Process -Id $active.pid -ErrorAction SilentlyContinue
    if ($existing) {
        throw "Capture is already running: pid=$($active.pid) dir=$($active.capture_dir)"
    }
    Remove-Item -LiteralPath $activePath -Force
}

$stamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$sessionRoot = Join-Path $RepoRoot ("tools-temp\live-pcaps\" + $Label)
$captureDir = Join-Path $sessionRoot $stamp
New-Item -ItemType Directory -Force -Path $captureDir | Out-Null

$rawPath = Join-Path $captureDir "raw.pcapng"
$stdoutPath = Join-Path $captureDir "tshark_stdout.log"
$stderrPath = Join-Path $captureDir "tshark_stderr.log"
$filter = "host $ServerHost"

$args = @("-i", $Interface, "-f", $filter, "-w", $rawPath, "-q")
$proc = Start-Process -FilePath $TsharkPath -ArgumentList $args -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath

$meta = [ordered]@{
    pid = $proc.Id
    status = "running"
    mode = $Mode
    label = $Label
    capture_dir = $captureDir
    raw_path = $rawPath
    server_host = $ServerHost
    interface = $Interface
    interface_name = "Ethernet"
    tshark_path = $TsharkPath
    capture_filter = $filter
    filter_mode_used = "host"
    started_utc = (Get-Date).ToUniversalTime().ToString("o")
}

$meta | ConvertTo-Json -Depth 6 | Out-File -LiteralPath (Join-Path $captureDir "capture_meta.json") -Encoding utf8
$meta | ConvertTo-Json -Depth 6 | Out-File -LiteralPath $activePath -Encoding utf8

"Started capture pid=$($proc.Id)"
"CaptureDir=$captureDir"
