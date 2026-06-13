param(
    [string]$RepoRoot = "",
    [string]$StarterPath = "",
    [string]$LogPath = "",
    [int]$TimeoutMinutes = 30,
    [switch]$DryRun,
    [switch]$AllowInject,
    [switch]$AllowWrite,
    [switch]$AllowCaptureStart,
    [switch]$AllowStopProcess,
    [switch]$AllowKillExistingInjector
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepoRoot {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

$RepoRoot = Resolve-RepoRoot $RepoRoot
if ([string]::IsNullOrWhiteSpace($StarterPath)) {
    $StarterPath = Join-Path $RepoRoot "tools-temp\start-local-ao-capture.ps1"
}
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $RepoRoot "tools-temp\live-capture-watch.log"
}

$startedAt = Get-Date

Write-Host "Resolved live capture watcher paths:"
Write-Host "  RepoRoot=$RepoRoot"
Write-Host "  StarterPath=$StarterPath"
Write-Host "  LogPath=$LogPath"
Write-Host "  TimeoutMinutes=$TimeoutMinutes"
Write-Host "  IntendedAction=watch for a fresh AnarchyOnline process, then invoke the capture starter"

$guardsSatisfied = $AllowInject -and $AllowCaptureStart -and $AllowWrite -and -not $DryRun
if (-not $guardsSatisfied) {
    Write-Host "Watcher not started. Pass -AllowInject -AllowCaptureStart -AllowWrite to allow a capture start."
    return
}

if (-not (Test-Path -LiteralPath $StarterPath -PathType Leaf)) {
    throw "Capture starter not found: $StarterPath"
}

Remove-Item -LiteralPath $LogPath -ErrorAction SilentlyContinue
"$(Get-Date -Format o) waiting for a fresh AnarchyOnline process started after $($startedAt.ToString('o'))" | Out-File -FilePath $LogPath -Encoding utf8

$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
while ((Get-Date) -lt $deadline) {
    $clients = @(Get-Process AnarchyOnline -ErrorAction SilentlyContinue |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.MainWindowTitle) -and
            $_.StartTime -ge $startedAt.AddSeconds(-2)
        })

    if ($clients.Count -eq 1) {
        "$(Get-Date -Format o) found pid=$($clients[0].Id) title=$($clients[0].MainWindowTitle)" | Add-Content -Path $LogPath
        $starterArgs = @(
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            $StarterPath,
            "-RepoRoot",
            $RepoRoot,
            "-TargetPid",
            $clients[0].Id,
            "-AllowInject",
            "-AllowCaptureStart",
            "-AllowWrite"
        )
        if ($AllowKillExistingInjector) { $starterArgs += "-AllowKillExistingInjector" }
        if ($AllowStopProcess) { $starterArgs += "-AllowStopProcess" }
        & powershell @starterArgs | Add-Content -Path $LogPath
        exit 0
    }

    if ($clients.Count -gt 1) {
        "$(Get-Date -Format o) multiple AO clients found" | Add-Content -Path $LogPath
        foreach ($client in $clients) {
            "pid=$($client.Id) title=$($client.MainWindowTitle)" | Add-Content -Path $LogPath
        }
        exit 2
    }

    Start-Sleep -Seconds 5
}

"$(Get-Date -Format o) timed out waiting for AO client" | Add-Content -Path $LogPath
exit 1
