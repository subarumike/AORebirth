param(
    [ValidateSet("ChatEngine", "LoginEngine", "ZoneEngine", "WebEngine")]
    [string[]]$EngineName
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $root "logs\engines"
$engineDir = Join-Path $root "AORebirth\Built\Debug"
$configPath = Join-Path $root "AORebirth\Config\Config.xml"
$statusProbe = Join-Path $root "Tools\engine_status_probe.js"
$cscript = Join-Path $env:SystemRoot "System32\cscript.exe"
$failed = $false

$engineDefinitions = @(
    @{ Name = "ZoneEngine"; File = "ZoneEngine.exe" },
    @{ Name = "WebEngine"; File = "WebEngine.exe" },
    @{ Name = "LoginEngine"; File = "LoginEngine.exe" },
    @{ Name = "ChatEngine"; File = "ChatEngine.exe" }
)

$engines = if ($EngineName -and $EngineName.Count -gt 0) {
    @($engineDefinitions | Where-Object { $EngineName -contains $_.Name })
}
else {
    @($engineDefinitions)
}

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
    $metadataIsTrusted = $false
    $managedStopVerified = $false
    $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $engineDir $engine.File))

    if (Test-Path $pidFile) {
        try {
            $metadata = Get-Content -Path $pidFile -Raw | ConvertFrom-Json
            $metadataProcess = Get-Process -Id ([int]$metadata.Pid) -ErrorAction SilentlyContinue
            if ($metadata.ShutdownFile) {
                $shutdownFile = [string]$metadata.ShutdownFile
            }

            if ($metadataProcess) {
                $actualPath = [System.IO.Path]::GetFullPath($metadataProcess.Path)
                $recordedPath = [System.IO.Path]::GetFullPath([string]$metadata.Path)
                $recordedStart = [DateTime]::Parse(
                    [string]$metadata.StartedAt,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [System.Globalization.DateTimeStyles]::RoundtripKind)
                $startDifferenceSeconds = [Math]::Abs(
                    ($metadataProcess.StartTime.ToUniversalTime() - $recordedStart.ToUniversalTime()).TotalSeconds)
                if ([string]$metadata.Engine -ieq $engine.Name -and
                    $actualPath -ieq $expectedPath -and
                    $recordedPath -ieq $expectedPath -and
                    $startDifferenceSeconds -le 5) {
                    $metadataIsTrusted = $true
                }
                else {
                    Write-Warning "$($engine.Name) PID metadata does not identify the expected repository executable; no process was stopped."
                    $failed = $true
                }
            }
            else {
                $metadataIsTrusted = $true
                $managedStopVerified = $true
            }
        }
        catch {
            Write-Warning "Could not safely validate PID metadata for $($engine.Name); no process was stopped."
            $failed = $true
        }
    }

    if ($metadataProcess -and $metadataIsTrusted) {
        try {
            Stop-EngineProcess -Process $metadataProcess -EngineName $engine.Name -ShutdownFile $shutdownFile
            $managedStopVerified = $true
        }
        catch {
            Write-Warning "$($engine.Name) managed PID could not be stopped safely."
            $failed = $true
        }
    }
    elseif (-not (Test-Path $pidFile)) {
        Write-Host "$($engine.Name) PID metadata process is not running."
    }

    if ($metadataIsTrusted -and $managedStopVerified -and (Test-Path $pidFile)) {
        Remove-Item -LiteralPath $pidFile -Force
    }

    if ($metadataIsTrusted -and $managedStopVerified -and (Test-Path $shutdownFile)) {
        Remove-Item -LiteralPath $shutdownFile -Force
    }

    & $cscript //nologo $statusProbe --config $configPath --engine-dir $engineDir --prestart $engine.Name
    $releaseExit = $LASTEXITCODE
    if ($releaseExit -ne 0) {
        Write-Warning "$($engine.Name) is not fully stopped with its ports released; no unmanaged process was killed."
        $failed = $true
    }
}

if ($failed) {
    Write-Error "AO Rebirth engine shutdown did not reach a fully verified state."
}

Write-Host "AO Rebirth engine shutdown complete."
