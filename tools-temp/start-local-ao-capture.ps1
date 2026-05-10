param(
    [string]$Title = "",
    [int]$TargetPid = 0,
    [string]$PluginPath = "C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"
)

$ErrorActionPreference = "Stop"

$injectorExe = "C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector.exe"
$injectorDir = Split-Path -Parent $injectorExe
$logPath = Join-Path $injectorDir "AOSharpLiveInjector.log"

Get-Process AOSharpLiveInjector -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -LiteralPath $logPath -ErrorAction SilentlyContinue

$args = @("--plugin", $PluginPath, "--log", $logPath)
if ($TargetPid -gt 0) {
    $args += @("--pid", $TargetPid)
}
if (-not [string]::IsNullOrWhiteSpace($Title)) {
    $args += @("--title", $Title)
}

$process = Start-Process -FilePath $injectorExe -ArgumentList $args -WorkingDirectory $injectorDir -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 3

$status = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
if ($status) {
    "AOSharpLiveInjector running pid=$($status.Id)"
} else {
    "AOSharpLiveInjector exited"
}

if (Test-Path $logPath) {
    Get-Content -Raw $logPath
}
