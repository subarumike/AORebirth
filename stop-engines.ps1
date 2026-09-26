param(
    [ValidateSet("ChatEngine", "LoginEngine", "ZoneEngine_New", "WebEngine")]
    [string[]]$EngineName,

    [switch]$CoreOnly,

    [switch]$StaleCheckoutsOnly,

    [switch]$IdentifyOnly
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $root "logs\engines"
$engineDir = Join-Path $root "AORebirth\Built\Debug"
$configPath = $env:AO_REBIRTH_CONFIG_PATH
if ([string]::IsNullOrWhiteSpace($configPath)) {
    $configPath = Join-Path $root "AORebirth\Config\Config.xml"
}
$statusProbe = Join-Path $root "Tools\engine_status_probe.js"
$cscript = Join-Path $env:SystemRoot "System32\cscript.exe"
$failed = $false
$coreEngineNames = @("ChatEngine", "LoginEngine", "ZoneEngine_New")

if ($CoreOnly -and $EngineName -and $EngineName.Count -gt 0) {
    throw "CoreOnly cannot be combined with EngineName."
}

$engineDefinitions = @(
    @{ Name = "ZoneEngine_New"; File = "ZoneEngine_New\ZoneEngine_New.exe" },
    @{ Name = "WebEngine"; File = "WebEngine.exe" },
    @{ Name = "LoginEngine"; File = "LoginEngine.exe" },
    @{ Name = "ChatEngine"; File = "ChatEngine.exe" }
)

$engines = if ($CoreOnly) {
    @($engineDefinitions | Where-Object { $coreEngineNames -contains $_.Name })
}
elseif ($EngineName -and $EngineName.Count -gt 0) {
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
        if (-not (Wait-ProcessExit -ProcessId $Process.Id -TimeoutSeconds 5)) {
            throw "$EngineName pid=$($Process.Id) remained running after confirmed force termination."
        }
    }
}

function Get-ProcessesByExecutablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExpectedPath
    )

    $normalizedExpectedPath = [System.IO.Path]::GetFullPath($ExpectedPath)
    @(
        foreach ($candidate in @(Get-Process -ErrorAction SilentlyContinue)) {
            try {
                $candidatePath = [System.IO.Path]::GetFullPath($candidate.Path)
                if ($candidatePath -ieq $normalizedExpectedPath) {
                    $candidate
                }
            }
            catch {
            }
        }
    )
}

function Get-AORebirthEngineProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Engine
    )

    if ($coreEngineNames -notcontains $Engine.Name) {
        return @()
    }

    $relativeEnginePath = Join-Path "AORebirth\Built\Debug" ([string]$Engine.File)
    $pathSuffix = [System.IO.Path]::DirectorySeparatorChar + $relativeEnginePath

    @(
        foreach ($candidate in @(Get-Process -ErrorAction SilentlyContinue)) {
            try {
                $candidatePath = [System.IO.Path]::GetFullPath($candidate.Path)
                if ($candidate.ProcessName -ine $Engine.Name) {
                    continue
                }
                if ([System.IO.Path]::GetFileNameWithoutExtension($candidatePath) -ine $Engine.Name) {
                    continue
                }
                if (-not $candidatePath.EndsWith($pathSuffix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    continue
                }

                $checkoutRootText = $candidatePath.Substring(0, $candidatePath.Length - $pathSuffix.Length)
                if ([string]::IsNullOrWhiteSpace($checkoutRootText)) {
                    continue
                }

                $checkoutRoot = [System.IO.Path]::GetFullPath($checkoutRootText)
                [pscustomobject]@{
                    Process = $candidate
                    Path = $candidatePath
                    CheckoutRoot = $checkoutRoot
                    ShutdownFile = Join-Path $checkoutRoot "logs\engines\$($Engine.Name).shutdown"
                    IsCurrentCheckout = $checkoutRoot -ieq [System.IO.Path]::GetFullPath($root)
                }
            }
            catch {
            }
        }
    )
}

function Wait-EnginePrestartState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EngineName,

        [switch]$AllowCurrentCheckoutOwner,

        [int]$TimeoutSeconds = 10
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $probeOutput = @()
    $probeExit = 1
    do {
        $probeOutput = @(& $cscript //nologo $statusProbe --config $configPath --engine-dir $engineDir --prestart $EngineName 2>&1)
        $probeExit = $LASTEXITCODE
        if ($probeExit -eq 0 -or ($AllowCurrentCheckoutOwner -and $probeExit -eq 3)) {
            $probeOutput | ForEach-Object { Write-Host $_ }
            return $probeExit
        }

        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    $probeOutput | ForEach-Object { Write-Host $_ }
    return $probeExit
}

foreach ($engine in @($engines | Where-Object { $coreEngineNames -contains $_.Name })) {
    foreach ($identified in @(Get-AORebirthEngineProcesses -Engine $engine)) {
        Write-Host ("[ACTIVE_CHECKOUT_WINS] identified engine={0} pid={1} checkout={2} path={3}" -f `
            $engine.Name, $identified.Process.Id, $identified.CheckoutRoot, $identified.Path)

        if ($IdentifyOnly) {
            continue
        }
        if ($StaleCheckoutsOnly -and $identified.IsCurrentCheckout) {
            continue
        }

        try {
            Stop-EngineProcess `
                -Process $identified.Process `
                -EngineName $engine.Name `
                -ShutdownFile $identified.ShutdownFile
            if (Test-Path -LiteralPath $identified.ShutdownFile) {
                Remove-Item -LiteralPath $identified.ShutdownFile -Force
            }
        }
        catch {
            Write-Warning ("Could not stop positively identified AORebirth {0} pid={1} from checkout {2}." -f `
                $engine.Name, $identified.Process.Id, $identified.CheckoutRoot)
            $failed = $true
        }
    }
}

if ($IdentifyOnly) {
    Write-Host "ACTIVE_CHECKOUT_WINS identification complete; no process was stopped."
    return
}

if ($StaleCheckoutsOnly) {
    foreach ($engine in @($engines | Where-Object { $coreEngineNames -contains $_.Name })) {
        $prestartExit = Wait-EnginePrestartState -EngineName $engine.Name -AllowCurrentCheckoutOwner
        if ($prestartExit -ne 0 -and $prestartExit -ne 3) {
            Write-Warning "$($engine.Name) pre-start state remains unsafe; an unknown or unverified owner was not killed."
            $failed = $true
        }
    }

    if ($failed) {
        Write-Error "AO Rebirth stale-checkout cleanup did not reach a verified safe state."
    }

    Write-Host "AO Rebirth stale-checkout cleanup complete."
    return
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
                    [System.IO.Path]::GetFullPath($shutdownFile) -ieq [System.IO.Path]::GetFullPath($defaultShutdownFile) -and
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
}

# Stop every selected managed process before checking released listeners.
foreach ($engine in $engines) {
    $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $engineDir $engine.File))
    if ($engine.Name -eq "ZoneEngine_New") {
        # Verify the exact backend path as well as released listener ownership.
        $stillRunning = @(Get-ProcessesByExecutablePath -ExpectedPath $expectedPath)
        if ($stillRunning) {
            Write-Warning "ZoneEngine_New is still running after stop (pid=$($stillRunning.Id -join ','))."
            $failed = $true
        }
        else {
            Write-Host "ZoneEngine_New process is not running."
        }
        $releaseExit = Wait-EnginePrestartState -EngineName "ZoneEngine_New"
        if ($releaseExit -ne 0) {
            Write-Warning "ZoneEngine_New zone port is not fully released; an unknown or unverified owner was not killed."
            $failed = $true
        }
    }
    else {
        $releaseExit = Wait-EnginePrestartState -EngineName $engine.Name
        if ($releaseExit -ne 0) {
            Write-Warning "$($engine.Name) is not fully stopped with its ports released; an unknown or unverified owner was not killed."
            $failed = $true
        }
    }
}

if ($failed) {
    Write-Error "AO Rebirth engine shutdown did not reach a fully verified state."
}

Write-Host "AO Rebirth engine shutdown complete."
