$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$engineDir = Join-Path $root "AORebirth\Built\Debug"
$logDir = Join-Path $root "logs\engines"

$engines = @(
    @{ Name = "ChatEngine"; File = "ChatEngine.exe"; Ports = @(6996, 7012) },
    @{ Name = "LoginEngine"; File = "LoginEngine.exe"; Ports = @(7500) },
    @{ Name = "ZoneEngine"; File = "ZoneEngine.exe"; Ports = @(7501) },
    @{ Name = "WebEngine"; File = "WebEngine.exe"; Ports = @(8181) }
)

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

function Get-EnginePath {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [string]$DefaultPath
    )

    try {
        if (-not [string]::IsNullOrWhiteSpace($Process.Path)) {
            return $Process.Path
        }
    }
    catch {
    }

    return $DefaultPath
}

$rows = foreach ($engine in $engines) {
    $pidFile = Join-Path $logDir "$($engine.Name).pid.json"
    $metadata = $null
    $metadataProcess = $null
    $fallbackProcesses = @()

    if (Test-Path $pidFile) {
        try {
            $metadata = Get-Content -Path $pidFile -Raw | ConvertFrom-Json
            $metadataProcess = Get-Process -Id ([int]$metadata.Pid) -ErrorAction SilentlyContinue
        }
        catch {
            $metadata = $null
            $metadataProcess = $null
        }
    }

    $fallbackProcesses = @(Get-Process -Name $engine.Name -ErrorAction SilentlyContinue)

    $processes = @()
    if ($metadataProcess) {
        $processes += $metadataProcess
    }

    foreach ($process in $fallbackProcesses) {
        if (-not ($processes | Where-Object { $_.Id -eq $process.Id })) {
            $processes += $process
        }
    }

    $expectedPath = Join-Path $engineDir $engine.File
    $pids = if ($processes.Count -gt 0) {
        ($processes | ForEach-Object { $_.Id }) -join ","
    }
    else {
        ""
    }

    $paths = if ($processes.Count -gt 0) {
        ($processes | ForEach-Object { Get-EnginePath -Process $_ -DefaultPath $expectedPath } | Select-Object -Unique) -join "; "
    }
    else {
        $expectedPath
    }

    $portStates = foreach ($port in $engine.Ports) {
        $state = if (Test-EnginePort -Port $port) { "Listening" } else { "Closed" }
        "$port=$state"
    }

    [pscustomobject]@{
        Engine = $engine.Name
        Running = ($processes.Count -gt 0)
        Pid = $pids
        Path = $paths
        Ports = ($portStates -join "; ")
        Metadata = if (Test-Path $pidFile) { $pidFile } else { "" }
    }
}

$rows | Format-Table -AutoSize
