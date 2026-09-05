param(
    [switch]$WithWeb,
    [switch]$WebOnly,
    [switch]$Visible,
    [switch]$NewZoneEngine,
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
$configPath = $env:AO_REBIRTH_CONFIG_PATH
if ([string]::IsNullOrWhiteSpace($configPath)) {
    $configPath = Join-Path $root "AORebirth\Config\Config.xml"
}
$logDir = Join-Path $root "logs\engines"
$statusProbe = Join-Path $root "Tools\engine_status_probe.js"
$cscript = Join-Path $env:SystemRoot "System32\cscript.exe"

if ($WithWeb -and $WebOnly) {
    throw "WithWeb and WebOnly cannot be combined."
}

if ($NewZoneEngine -and $WebOnly) {
    throw "NewZoneEngine and WebOnly cannot be combined."
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

    $probeArguments = @("--config", $configPath, "--engine-dir", $engineDir) + $Arguments

    if ($Quiet) {
        & $cscript //nologo $statusProbe @probeArguments *> $null
        $probeExit = $LASTEXITCODE
    }
    else {
        $probeOutput = @(& $cscript //nologo $statusProbe @probeArguments)
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
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [int[]]$Ports
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()
        if ($Process.HasExited) {
            return $false
        }

        if ($EngineName -eq "ZoneEngine_New") {
            $ownsEveryPort = $true
            foreach ($port in $Ports) {
                $ownedListeners = @(
                    Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue |
                        Where-Object { $_.OwningProcess -eq $Process.Id }
                )
                if ($ownedListeners.Count -eq 0) {
                    $ownsEveryPort = $false
                    break
                }
            }

            if ($ownsEveryPort) {
                return $true
            }
        }
        else {
            $probeExit = Invoke-EngineStatusProbe -Arguments @(
                "--engine-required",
                $EngineName,
                "--expect-pid",
                "$EngineName=$($Process.Id)"
            ) -Quiet
            if ($probeExit -eq 0) {
                return $true
            }
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

$zoneEngine = if ($NewZoneEngine) {
    @{ Name = "ZoneEngine_New"; File = "ZoneEngine_New\ZoneEngine_New.exe"; Ports = @(7501) }
}
else {
    @{ Name = "ZoneEngine"; File = "ZoneEngine.exe"; Ports = @(7501) }
}

$coreEngines = @(
    @{ Name = "ChatEngine"; File = "ChatEngine.exe"; Ports = @(6996, 7012) },
    @{ Name = "LoginEngine"; File = "LoginEngine.exe"; Ports = @(7500) },
    $zoneEngine
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

    if ($processName -eq "ZoneEngine_New") {
        $existingProcesses = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
        $existingListeners = @(
            foreach ($port in $engine.Ports) {
                Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
            }
        )
        if ($existingProcesses.Count -gt 0 -or $existingListeners.Count -gt 0) {
            $failures.Add("ZoneEngine_New pre-start check requires no existing process and a free zone port.")
            break
        }
    }
    else {
        $prestartExit = Invoke-EngineStatusProbe -Arguments @("--prestart", $processName)
        if ($prestartExit -eq 3) {
            Write-Host "$($engine.File) is already running with verified executable and port ownership."
            continue
        }
        if ($prestartExit -ne 0) {
            $failures.Add("$processName pre-start ownership check failed with exit code $prestartExit.")
            break
        }
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

    $workingDirectory = Split-Path -Parent $exePath
    if ($Visible) {
        $process = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $workingDirectory -WindowStyle $windowStyle -PassThru
    }
    else {
        $process = Start-HiddenEngineProcess -ExePath $exePath -Arguments $arguments -WorkingDirectory $workingDirectory
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

    if (-not (Wait-EngineOwnership -EngineName $processName -TimeoutSeconds $StartupTimeoutSeconds -Process $process -Ports $engine.Ports)) {
        $failures.Add("$processName did not establish exact PID ownership of every configured port within $StartupTimeoutSeconds seconds.")
        break
    }

    if ($processName -eq "ZoneEngine_New") {
        Write-Host "[AORebirth Status] engine=ZoneEngine_New processPid=$($process.Id) port=7501 ownership=PASS"
    }
    else {
        [void](Invoke-EngineStatusProbe -Arguments @(
            "--engine-required",
            $processName,
            "--expect-pid",
            "$processName=$($process.Id)"
        ))
    }
}

if ($failures.Count -eq 0) {
    if ($WebOnly) {
        $finalStatus = Invoke-EngineStatusProbe -Arguments @("--web-required")
    }
    elseif ($NewZoneEngine) {
        $finalStatus = Invoke-EngineStatusProbe -Arguments @("--engine-required", "ChatEngine")
        if ($finalStatus -eq 0) {
            $finalStatus = Invoke-EngineStatusProbe -Arguments @("--engine-required", "LoginEngine")
        }

        $newZoneProcesses = @(Get-Process -Name "ZoneEngine_New" -ErrorAction SilentlyContinue)
        $newZoneListeners = @(Get-NetTCPConnection -State Listen -LocalPort 7501 -ErrorAction SilentlyContinue)
        if (($newZoneProcesses.Count -ne 1) -or
            ($newZoneListeners.Count -ne 1) -or
            ($newZoneListeners[0].OwningProcess -ne $newZoneProcesses[0].Id)) {
            $finalStatus = 1
        }
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
            if ($entry.Engine -eq "ZoneEngine_New") {
                if (Get-Process -Name "ZoneEngine_New" -ErrorAction SilentlyContinue) {
                    $failures.Add("ZoneEngine_New cleanup did not stop its managed process.")
                }
                if (Get-NetTCPConnection -State Listen -LocalPort 7501 -ErrorAction SilentlyContinue) {
                    $failures.Add("ZoneEngine_New cleanup did not release port 7501.")
                }
            }
            else {
                $releasedExit = Invoke-EngineStatusProbe -Arguments @("--prestart", $entry.Engine)
                if ($releasedExit -ne 0) {
                    $failures.Add("$($entry.Engine) cleanup did not release its managed process and ports.")
                }
            }
        }
        catch {
            $failures.Add("$($entry.Engine) cleanup ownership verification could not run.")
        }
    }

    Write-Error ("AO Rebirth engine startup failed: " + ($failures -join " "))
}

Write-Host "AO Rebirth engine startup complete."
