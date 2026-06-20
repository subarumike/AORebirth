$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $root "logs\engines"

$engines = @(
    @{ Name = "ZoneEngine"; File = "ZoneEngine.exe" },
    @{ Name = "WebEngine"; File = "WebEngine.exe" },
    @{ Name = "LoginEngine"; File = "LoginEngine.exe" },
    @{ Name = "ChatEngine"; File = "ChatEngine.exe" }
)

function Wait-ProcessExit {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return -not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)
}

function Stop-EngineProcess {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [string]$EngineName,

        [string]$ShutdownFile
    )

    Write-Host "Stopping $EngineName pid=$($Process.Id)"

    if (-not [string]::IsNullOrWhiteSpace($ShutdownFile)) {
        $shutdownDir = Split-Path -Parent $ShutdownFile
        if (-not (Test-Path $shutdownDir)) {
            New-Item -Path $shutdownDir -ItemType Directory | Out-Null
        }

        "stop requested $(Get-Date -Format o)" | Set-Content -Path $ShutdownFile -Encoding UTF8
        if (Wait-ProcessExit -ProcessId $Process.Id -TimeoutSeconds 15) {
            Write-Host "$EngineName stopped after shutdown request."
            return
        }
    }

    try {
        $Process.Refresh()
        if ($Process.MainWindowHandle -ne 0) {
            [void]$Process.CloseMainWindow()
            if (Wait-ProcessExit -ProcessId $Process.Id -TimeoutSeconds 5) {
                Write-Host "$EngineName stopped after close-window request."
                return
            }
        }
    }
    catch {
    }

    if (Get-Process -Id $Process.Id -ErrorAction SilentlyContinue) {
        Write-Warning "$EngineName did not exit cleanly; forcing process stop."
        Stop-Process -Id $Process.Id -Force
    }
}

foreach ($engine in $engines) {
    $pidFile = Join-Path $logDir "$($engine.Name).pid.json"
    $defaultShutdownFile = Join-Path $logDir "$($engine.Name).shutdown"
    $metadataProcess = $null
    $shutdownFile = $defaultShutdownFile

    if (Test-Path $pidFile) {
        try {
            $metadata = Get-Content -Path $pidFile -Raw | ConvertFrom-Json
            $metadataProcess = Get-Process -Id ([int]$metadata.Pid) -ErrorAction SilentlyContinue
            if ($metadata.ShutdownFile) {
                $shutdownFile = [string]$metadata.ShutdownFile
            }
        }
        catch {
            Write-Warning "Could not read PID metadata for $($engine.Name): $($_.Exception.Message)"
        }
    }

    if ($metadataProcess) {
        Stop-EngineProcess -Process $metadataProcess -EngineName $engine.Name -ShutdownFile $shutdownFile
    }
    else {
        Write-Host "$($engine.Name) PID metadata process is not running."
    }

    $fallbackProcesses = @(Get-Process -Name $engine.Name -ErrorAction SilentlyContinue)
    foreach ($process in $fallbackProcesses) {
        if ($metadataProcess -and $process.Id -eq $metadataProcess.Id) {
            continue
        }

        Stop-EngineProcess -Process $process -EngineName $engine.Name -ShutdownFile $null
    }

    if (Test-Path $pidFile) {
        Remove-Item -LiteralPath $pidFile -Force
    }

    if (Test-Path $shutdownFile) {
        Remove-Item -LiteralPath $shutdownFile -Force
    }
}

Write-Host "AO Rebirth engine shutdown complete."
