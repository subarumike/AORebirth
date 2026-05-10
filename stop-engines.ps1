$ErrorActionPreference = "Stop"

$engineNames = @(
    "ZoneEngine",
    "LoginEngine",
    "ChatEngine",
    "WebEngine"
)

foreach ($name in $engineNames) {
    $processes = Get-Process -Name $name -ErrorAction SilentlyContinue

    if (-not $processes) {
        Write-Host "$name is not running."
        continue
    }

    foreach ($process in $processes) {
        Write-Host "Stopping $($process.ProcessName) pid=$($process.Id)"
        Stop-Process -Id $process.Id -Force
    }
}

Write-Host "CellAO engine shutdown complete."
