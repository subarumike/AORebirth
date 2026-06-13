param(
    [string]$ServerHost = "199.241.136.157",
    [string]$Interface = "4",
    [string]$Label = "private-server-auto",
    [ValidateSet("All", "Loot", "Quest", "PacketsOnly")]
    [string]$Mode = "All",
    [string]$RepoRoot = "",
    [string]$OutputRoot = "",
    [string]$TsharkPath = "C:\Program Files\Wireshark\tshark.exe",
    [switch]$DryRun,
    [switch]$AllowWrite,
    [switch]$AllowCaptureStart
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
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RepoRoot "tools-temp\live-pcaps"
}

$collectorRoot = Join-Path $RepoRoot "tools-temp\live-data-collector"
$activePath = Join-Path $collectorRoot "active_capture.json"
$stamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$sessionRoot = Join-Path $OutputRoot $Label
$captureDir = Join-Path $sessionRoot $stamp
$rawPath = Join-Path $captureDir "raw.pcapng"
$stdoutPath = Join-Path $captureDir "tshark_stdout.log"
$stderrPath = Join-Path $captureDir "tshark_stderr.log"
$filter = "host $ServerHost"

Write-Host "Resolved live data capture paths:"
Write-Host "  RepoRoot=$RepoRoot"
Write-Host "  CollectorRoot=$collectorRoot"
Write-Host "  ActivePath=$activePath"
Write-Host "  OutputRoot=$OutputRoot"
Write-Host "  CaptureDir=$captureDir"
Write-Host "  RawPath=$rawPath"
Write-Host "  TsharkPath=$TsharkPath"
Write-Host "  CaptureFilter=$filter"
Write-Host "  IntendedAction=start tshark capture"

if ($DryRun -or -not ($AllowCaptureStart -and $AllowWrite)) {
    Write-Host "No capture started and no files written. Pass -AllowCaptureStart -AllowWrite to start tshark."
    return
}

if (-not (Test-Path -LiteralPath $TsharkPath -PathType Leaf)) {
    throw "tshark not found: $TsharkPath"
}

if (Test-Path -LiteralPath $activePath) {
    $active = Get-Content -Raw -LiteralPath $activePath | ConvertFrom-Json
    $existing = Get-Process -Id $active.pid -ErrorAction SilentlyContinue
    if ($existing) {
        throw "Capture is already running: pid=$($active.pid) dir=$($active.capture_dir)"
    }
    Remove-Item -LiteralPath $activePath -Force
}

New-Item -ItemType Directory -Force -Path $captureDir | Out-Null

$args = @("-i", $Interface, "-f", ('"{0}"' -f $filter), "-w", ('"{0}"' -f $rawPath), "-q")
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
