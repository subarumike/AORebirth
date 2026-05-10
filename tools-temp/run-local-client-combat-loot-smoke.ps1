param(
    [string]$Username = "Mike",
    [string]$Password = "123456",
    [string]$CharacterTitle = "Mikedoc",
    [int]$LoginTimeoutSeconds = 75,
    [int]$SmokeTimeoutSeconds = 120,
    [switch]$RestartEngines,
    [switch]$CleanStart,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repo = "C:\Users\Mike\Documents\Cellao-Clean"
$clientDir = "C:\Funcom\Anarchy Online"
$clientExe = Join-Path $clientDir "AnarchyOnline.exe"
$runner = Join-Path $repo "tools-temp\run-local-combat-loot-smoke.ps1"
$engineStart = Join-Path $repo "start-engines.ps1"
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
$solution = Join-Path $repo "CellAO\CellAO.sln"

function Get-AOClientLaunchDiagnostics {
    param(
        [int]$ProcessId
    )

    $parts = New-Object System.Collections.Generic.List[string]
    if ($ProcessId -gt 0) {
        $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($process) {
            $parts.Add("process still running pid=$ProcessId title='$($process.MainWindowTitle)' responding=$($process.Responding)")
        }
        else {
            $parts.Add("process exited pid=$ProcessId")
        }
    }

    $disconnectReport = Join-Path $clientDir "DisconnectReport.txt"
    if (Test-Path $disconnectReport) {
        $report = Get-Item $disconnectReport
        $parts.Add("DisconnectReport LastWriteTime=$($report.LastWriteTime)")
        $tail = Get-Content -Path $disconnectReport -Tail 12 -ErrorAction SilentlyContinue
        if ($tail) {
            $parts.Add("DisconnectReport tail: $($tail -join ' | ')")
        }
    }

    return $parts -join " ; "
}

if ($CleanStart) {
    Get-Process AOSharpLiveInjector -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process AnarchyOnline -ErrorAction SilentlyContinue | Stop-Process -Force
}

if ($RestartEngines) {
    Get-Process ChatEngine,LoginEngine,ZoneEngine,WebEngine,MSBuild -ErrorAction SilentlyContinue | Stop-Process -Force
    if (-not $NoBuild) {
        & $msbuild $solution /t:Build /p:Configuration=Debug /m | Write-Host
    }
    powershell -NoProfile -ExecutionPolicy Bypass -File $engineStart | Write-Host
}

$client = Get-Process AnarchyOnline -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -like "*$CharacterTitle*" } |
    Select-Object -First 1

if (-not $client) {
    if (-not (Test-Path $clientExe)) {
        throw "AO client executable not found: $clientExe"
    }

    $launchedClient = Start-Process -FilePath $clientExe -WorkingDirectory $clientDir -PassThru

    $loginWindow = $null
    $deadline = (Get-Date).AddSeconds(25)
    while ((Get-Date) -lt $deadline -and -not $loginWindow) {
        Start-Sleep -Milliseconds 500
        $loginWindow = Get-Process AnarchyOnline -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowTitle -eq "Anarchy Online" } |
            Select-Object -First 1
    }

    if (-not $loginWindow) {
        throw "AO login window did not appear. $(Get-AOClientLaunchDiagnostics -ProcessId $launchedClient.Id)"
    }

    $ws = New-Object -ComObject WScript.Shell
    $null = $ws.AppActivate("Anarchy Online")
    Start-Sleep -Milliseconds 500
    $ws.SendKeys("$Username{TAB}$Password{ENTER}")

    $deadline = (Get-Date).AddSeconds($LoginTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
        $client = Get-Process AnarchyOnline -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowTitle -like "*$CharacterTitle*" } |
            Select-Object -First 1

        if ($client) {
            break
        }
    }

    if (-not $client) {
        $titles = Get-Process AnarchyOnline -ErrorAction SilentlyContinue |
            Select-Object Id,MainWindowTitle |
            ForEach-Object { "pid=$($_.Id) title=$($_.MainWindowTitle)" }
        throw "AO did not reach character window '*$CharacterTitle*' within $LoginTimeoutSeconds seconds. Seen: $($titles -join '; ')"
    }
}

Write-Host "AO client ready: pid=$($client.Id) title=$($client.MainWindowTitle)"

$smokeArgs = @(
    "-NoBuild",
    "-Title", $CharacterTitle,
    "-TargetPid", $client.Id,
    "-TimeoutSeconds", $SmokeTimeoutSeconds
)

if (-not $NoBuild) {
    $smokeArgs = @(
        "-Title", $CharacterTitle,
        "-TargetPid", $client.Id,
        "-TimeoutSeconds", $SmokeTimeoutSeconds
    )
}

& powershell -NoProfile -ExecutionPolicy Bypass -File $runner @smokeArgs
