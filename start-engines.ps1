param(
    [switch]$WithWeb,
    [switch]$WebOnly,
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
$statusProbe = Join-Path $root "Tools\engine_status_probe.js"
$cscript = Join-Path $env:SystemRoot "System32\cscript.exe"

if ($WithWeb -and $WebOnly) {
    throw "WithWeb and WebOnly cannot be combined."
}

if (-not (Test-Path $engineDir)) {
    throw "Engine build folder not found: $engineDir"
}

if (-not (Test-Path $logDir)) {
    New-Item -Path $logDir -ItemType Directory | Out-Null
}

if (-not (Test-Path $statusProbe)) {
    throw "Engine ownership probe not found: $statusProbe"
}

if (-not (Test-Path $cscript)) {
    throw "Windows Script Host was not found: $cscript"
}

function Invoke-EngineStatusProbe {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$Quiet
    )

    if ($Quiet) {
        & $cscript //nologo $statusProbe @Arguments *> $null
        $probeExit = $LASTEXITCODE
    }
    else {
        $probeOutput = @(& $cscript //nologo $statusProbe @Arguments)
        $probeExit = $LASTEXITCODE
        foreach ($line in $probeOutput) {
            Write-Host $line
        }
    }

    return $probeExit
}

function Wait-EngineOwnership {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EngineName,

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

        $probeExit = Invoke-EngineStatusProbe -Arguments @(
            "--engine-required",
            $EngineName,
            "--expect-pid",
            "$EngineName=$($Process.Id)"
        ) -Quiet
        if ($probeExit -eq 0) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

function Wait-ProcessExit {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()
        if ($Process.HasExited) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    $Process.Refresh()
    return $Process.HasExited
}

function Stop-LaunchedEngineProcess {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [string]$EngineName,

        [Parameter(Mandatory = $true)]
        [string]$ShutdownFile
    )

    if ($Process.HasExited) {
        return
    }

    "stop requested $(Get-Date -Format o)" | Set-Content -Path $ShutdownFile -Encoding UTF8
    if (Wait-ProcessExit -Process $Process -TimeoutSeconds 15) {
        return
    }

    Write-Warning "$EngineName pid=$($Process.Id) did not stop after its private shutdown request; stopping that exact launched PID."
    Stop-Process -Id $Process.Id -Force
    if (-not (Wait-ProcessExit -Process $Process -TimeoutSeconds 5)) {
        throw "Could not stop launched $EngineName pid=$($Process.Id)."
    }
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

$coreEngines = @(
    @{ Name = "ChatEngine"; File = "ChatEngine.exe"; Ports = @(6996, 7012) },
    @{ Name = "LoginEngine"; File = "LoginEngine.exe"; Ports = @(7500) },
    @{ Name = "ZoneEngine"; File = "ZoneEngine.exe"; Ports = @(7501) }
)
$webEngine = @{ Name = "WebEngine"; File = "WebEngine.exe"; Ports = @(8181) }

$engines = if ($WebOnly) { @($webEngine) } else { @($coreEngines) }

$windowStyle = if ($Visible) { "Normal" } else { "Hidden" }

if ($WithWeb) {
    $engines += $webEngine
}

$failures = New-Object System.Collections.Generic.List[string]
$launched = New-Object System.Collections.Generic.List[object]

try {
foreach ($engine in $engines) {
    $exePath = Join-Path $engineDir $engine.File
    $processName = $engine.Name
    $stdoutLog = Join-Path $logDir "$processName.out.log"
    $stderrLog = Join-Path $logDir "$processName.err.log"
    $pidFile = Join-Path $logDir "$processName.pid.json"
    $shutdownFile = Join-Path $logDir "$processName.shutdown"

    if (-not (Test-Path $exePath)) {
        $failures.Add("Missing $($engine.File); build the solution first.")
        break
    }

    $prestartExit = Invoke-EngineStatusProbe -Arguments @("--prestart", $processName)
    if ($prestartExit -eq 3) {
        Write-Host "$($engine.File) is already running with verified executable and port ownership."
        continue
    }
    if ($prestartExit -ne 0) {
        $failures.Add("$processName pre-start ownership check failed with exit code $prestartExit.")
        break
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

    $launched.Add(
        [pscustomobject]@{
            Engine = $processName
            Process = $process
            PidFile = $pidFile
            ShutdownFile = $shutdownFile
        })

    Write-Host "Started $($engine.File) pid=$($process.Id)"

    if (-not (Wait-EngineOwnership -EngineName $processName -TimeoutSeconds $StartupTimeoutSeconds -Process $process)) {
        $failures.Add("$processName did not establish exact PID ownership of every configured port within $StartupTimeoutSeconds seconds.")
        break
    }

    [void](Invoke-EngineStatusProbe -Arguments @(
        "--engine-required",
        $processName,
        "--expect-pid",
        "$processName=$($process.Id)"
    ))
}

if ($failures.Count -eq 0) {
    if ($WebOnly) {
        $finalStatus = Invoke-EngineStatusProbe -Arguments @("--web-required")
    }
    else {
        $finalStatus = Invoke-EngineStatusProbe -Arguments @("--core")
        if ($finalStatus -eq 0 -and $WithWeb) {
            $finalStatus = Invoke-EngineStatusProbe -Arguments @("--web-required")
        }
    }

    if ($finalStatus -ne 0) {
        $failures.Add("Final PID-to-port ownership verification failed with exit code $finalStatus.")
    }
}
}
catch {
    $failures.Add("An unexpected engine startup operation failed.")
}

if ($failures.Count -gt 0) {
    for ($index = $launched.Count - 1; $index -ge 0; $index--) {
        $entry = $launched[$index]
        $rollbackStopped = $false
        try {
            Stop-LaunchedEngineProcess -Process $entry.Process -EngineName $entry.Engine -ShutdownFile $entry.ShutdownFile
            $rollbackStopped = $true
        }
        catch {
            $failures.Add("Could not stop exact launched $($entry.Engine) pid=$($entry.Process.Id) during rollback.")
        }
        finally {
            if ($rollbackStopped -and (Test-Path $entry.PidFile)) {
                Remove-Item -LiteralPath $entry.PidFile -Force
            }
            if ($rollbackStopped -and (Test-Path $entry.ShutdownFile)) {
                Remove-Item -LiteralPath $entry.ShutdownFile -Force
            }
        }

        try {
            $releasedExit = Invoke-EngineStatusProbe -Arguments @("--prestart", $entry.Engine)
            if ($releasedExit -ne 0) {
                $failures.Add("$($entry.Engine) cleanup did not release its managed process and ports.")
            }
        }
        catch {
            $failures.Add("$($entry.Engine) cleanup ownership verification could not run.")
        }
    }

    Write-Error ("AO Rebirth engine startup failed: " + ($failures -join " "))
}

Write-Host "AO Rebirth engine startup complete."
