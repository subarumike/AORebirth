param(
    [switch]$WithWeb,
    [switch]$Visible,
    [int]$StartupTimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

$processPath = [System.Environment]::GetEnvironmentVariable("Path", "Process")
if ([string]::IsNullOrEmpty($processPath)) {
    $processPath = [System.Environment]::GetEnvironmentVariable("PATH", "Process")
}

[System.Environment]::SetEnvironmentVariable("PATH", $null, "Process")
if (-not [string]::IsNullOrEmpty($processPath)) {
    [System.Environment]::SetEnvironmentVariable("Path", $processPath, "Process")
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$engineDir = Join-Path $root "AORebirth\Built\Debug"
$logDir = Join-Path $root "logs\engines"

if (-not (Test-Path $engineDir)) {
    throw "Engine build folder not found: $engineDir"
}

if (-not (Test-Path $logDir)) {
    New-Item -Path $logDir -ItemType Directory | Out-Null
}

function Test-EnginePort {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connect = $client.ConnectAsync("127.0.0.1", $Port)
        if (-not $connect.Wait(500)) {
            return $false
        }

        return $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Close()
    }
}

function Wait-EnginePort {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds,

        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()
        if ($Process.HasExited) {
            return $false
        }

        if (Test-EnginePort -Port $Port) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

function Start-HiddenEngineProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    $process = Start-Process `
        -FilePath $ExePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -WindowStyle Hidden `
        -PassThru `
        -ErrorAction Stop

    Start-Sleep -Milliseconds 250
    $process.Refresh()
    if ($process.HasExited) {
        throw "Started $ExePath as pid=$($process.Id), but the process exited immediately."
    }

    return $process
}

$engines = @(
    @{ Name = "ChatEngine"; File = "ChatEngine.exe"; Ports = @(6996, 7012) },
    @{ Name = "LoginEngine"; File = "LoginEngine.exe"; Ports = @(7500) },
    @{ Name = "ZoneEngine"; File = "ZoneEngine.exe"; Ports = @(7501) }
)

$windowStyle = if ($Visible) { "Normal" } else { "Hidden" }

if ($WithWeb) {
    $engines += @{ Name = "WebEngine"; File = "WebEngine.exe"; Ports = @(8181) }
}

$failures = New-Object System.Collections.Generic.List[string]

foreach ($engine in $engines) {
    $exePath = Join-Path $engineDir $engine.File
    $processName = $engine.Name
    $stdoutLog = Join-Path $logDir "$processName.out.log"
    $stderrLog = Join-Path $logDir "$processName.err.log"
    $pidFile = Join-Path $logDir "$processName.pid.json"
    $shutdownFile = Join-Path $logDir "$processName.shutdown"

    if (-not (Test-Path $exePath)) {
        $failures.Add("Missing $($engine.File); build the solution first.")
        continue
    }

    $existing = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "$($engine.File) is already running."
        continue
    }

    if (Test-Path $shutdownFile) {
        Remove-Item -LiteralPath $shutdownFile -Force
    }

    $arguments = if ($Visible) {
        @("/autostart", "/shutdown-file", $shutdownFile)
    }
    else {
        @("/headless", "/shutdown-file", $shutdownFile, "/stdout-log", $stdoutLog, "/stderr-log", $stderrLog)
    }

    if ($Visible) {
        $process = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $engineDir -WindowStyle $windowStyle -PassThru
    }
    else {
        $process = Start-HiddenEngineProcess -ExePath $exePath -Arguments $arguments -WorkingDirectory $engineDir
    }

    $metadata = [ordered]@{
        Engine = $processName
        Pid = $process.Id
        Path = $exePath
        StartedAt = (Get-Date).ToString("o")
        Visible = [bool]$Visible
        Arguments = $arguments
        Ports = $engine.Ports
        StandardOutput = if ($Visible) { $null } else { $stdoutLog }
        StandardError = if ($Visible) { $null } else { $stderrLog }
        ShutdownFile = $shutdownFile
    }

    $metadata | ConvertTo-Json -Depth 4 | Set-Content -Path $pidFile -Encoding UTF8

    Write-Host "Started $($engine.File) pid=$($process.Id)"

    foreach ($port in $engine.Ports) {
        if (Wait-EnginePort -Port $port -TimeoutSeconds $StartupTimeoutSeconds -Process $process) {
            Write-Host "$processName port $port is listening."
        }
        else {
            $failures.Add("$processName did not open port $port within $StartupTimeoutSeconds seconds.")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Error ("AO Rebirth engine startup failed: " + ($failures -join " "))
}

Write-Host "AO Rebirth engine startup complete."
