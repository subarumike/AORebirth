param(
    [switch]$WithWeb,
    [switch]$Visible
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$engineDir = Join-Path $root "AORebirth\Built\Debug"

if (-not (Test-Path $engineDir)) {
    throw "Engine build folder not found: $engineDir"
}

$engines = @(
    @{ File = "ChatEngine.exe"; Arguments = "/autostart" },
    @{ File = "LoginEngine.exe"; Arguments = "/autostart" },
    @{ File = "ZoneEngine.exe"; Arguments = "/autostart" }
)

$windowStyle = if ($Visible) { "Normal" } else { "Hidden" }

if ($WithWeb) {
    $engines += @{ File = "WebEngine.exe"; Arguments = "" }
}

foreach ($engine in $engines) {
    $exePath = Join-Path $engineDir $engine.File
    $processName = [System.IO.Path]::GetFileNameWithoutExtension($engine.File)

    if (-not (Test-Path $exePath)) {
        Write-Warning "Missing $($engine.File); build the solution first."
        continue
    }

    if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
        Write-Host "$($engine.File) is already running."
        continue
    }

    if ([string]::IsNullOrWhiteSpace($engine.Arguments)) {
        Start-Process -FilePath $exePath -WorkingDirectory $engineDir -WindowStyle $windowStyle
    }
    else {
        Start-Process -FilePath $exePath -ArgumentList $engine.Arguments -WorkingDirectory $engineDir -WindowStyle $windowStyle
    }
    Write-Host "Started $($engine.File)"
    Start-Sleep -Milliseconds 500
}

Write-Host "AO Rebirth engine startup complete."
