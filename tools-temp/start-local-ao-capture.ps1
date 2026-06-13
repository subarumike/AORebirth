param(
    [string]$Title = "",
    [int]$TargetPid = 0,
    [string]$RepoRoot = "",
    [string]$PluginPath = "",
    [string]$InjectorPath = "",
    [string]$LogPath = "",
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

if ([string]::IsNullOrWhiteSpace($PluginPath)) {
    $PluginPath = Join-Path $RepoRoot "tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"
}

if ([string]::IsNullOrWhiteSpace($InjectorPath)) {
    $InjectorPath = Join-Path $RepoRoot "tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector.exe"
}

$injectorDir = Split-Path -Parent $InjectorPath
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $injectorDir "AOSharpLiveInjector.log"
}

$args = @("--plugin", $PluginPath, "--log", $LogPath)
if ($TargetPid -gt 0) {
    $args += @("--pid", $TargetPid)
}
if (-not [string]::IsNullOrWhiteSpace($Title)) {
    $args += @("--title", $Title)
}

Write-Host "Resolved live AO capture paths:"
Write-Host "  RepoRoot=$RepoRoot"
Write-Host "  InjectorPath=$InjectorPath"
Write-Host "  PluginPath=$PluginPath"
Write-Host "  LogPath=$LogPath"
Write-Host "  TargetPid=$TargetPid"
Write-Host "  Title=$Title"
Write-Host "  IntendedAction=inject AOSharpLiveCapture through AOSharpLiveInjector"

$guardsSatisfied = $AllowInject -and $AllowCaptureStart -and $AllowWrite -and -not $DryRun
if (-not $guardsSatisfied) {
    Write-Host "No injection performed. Pass -AllowInject -AllowCaptureStart -AllowWrite to start capture injection."
    return
}

if ($TargetPid -le 0 -and [string]::IsNullOrWhiteSpace($Title)) {
    throw "Pass -TargetPid or -Title so the injector target is explicit."
}

if (-not (Test-Path -LiteralPath $InjectorPath -PathType Leaf)) {
    throw "AOSharpLiveInjector not found: $InjectorPath"
}

if (-not (Test-Path -LiteralPath $PluginPath -PathType Leaf)) {
    throw "AOSharpLiveCapture plugin not found: $PluginPath"
}

$existingInjectors = @(Get-Process AOSharpLiveInjector -ErrorAction SilentlyContinue)
if ($existingInjectors.Count -gt 0) {
    if (-not ($AllowKillExistingInjector -and $AllowStopProcess)) {
        throw "Existing AOSharpLiveInjector process found. Pass -AllowKillExistingInjector -AllowStopProcess to stop it before injection."
    }

    foreach ($existing in $existingInjectors) {
        Stop-Process -Id $existing.Id -Force
    }
}

if (Test-Path -LiteralPath $LogPath) {
    Remove-Item -LiteralPath $LogPath -ErrorAction SilentlyContinue
}

$process = Start-Process -FilePath $InjectorPath -ArgumentList $args -WorkingDirectory $injectorDir -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 3

$status = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
if ($status) {
    "AOSharpLiveInjector running pid=$($status.Id)"
} else {
    "AOSharpLiveInjector exited"
}

if (Test-Path -LiteralPath $LogPath) {
    Get-Content -Raw -LiteralPath $LogPath
}
