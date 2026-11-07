[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$PackageRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$executable = Join-Path $PackageRoot 'AORebirth.exe'
$logRoot = Join-Path $repositoryRoot ('build-verify\windows-local-package-startup\' +
    [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))

function Invoke-PackageCommand {
    param([string]$Argument, [string]$Name, [int]$TimeoutSeconds)
    $stdoutPath = Join-Path $logRoot ($Name + '.stdout.log')
    $stderrPath = Join-Path $logRoot ($Name + '.stderr.log')
    $stdout = [IO.File]::Open($stdoutPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    $stderr = [IO.File]::Open($stderrPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    $process = New-Object Diagnostics.Process
    $clock = [Diagnostics.Stopwatch]::StartNew()
    try {
        $process.StartInfo.FileName = $executable
        $process.StartInfo.Arguments = $Argument
        $process.StartInfo.WorkingDirectory = $PackageRoot
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        if (-not $process.Start()) { throw 'The packaged verifier did not start.' }
        $ownedPid = $process.Id
        $stdoutCopy = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
        $stderrCopy = $process.StandardError.BaseStream.CopyToAsync($stderr)
        $timedOut = $false
        while (-not $process.WaitForExit(1000)) {
            if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                $timedOut = $true
                # This exact handle belongs to the verifier launched above. Its
                # backend services are stopped separately through their existing
                # installation-owned PID/start-time/executable identity records.
                if (-not $process.HasExited) { $process.Kill() }
                if (-not $process.WaitForExit(10000)) { throw 'The owned verifier process did not exit after its deadline.' }
                break
            }
        }
        $process.WaitForExit()
        $null = $stdoutCopy.GetAwaiter().GetResult()
        $null = $stderrCopy.GetAwaiter().GetResult()
        return [ordered]@{
            argument = $Argument
            pid = $ownedPid
            actualExitCode = $process.ExitCode
            timedOut = $timedOut
            timeoutSeconds = $TimeoutSeconds
            elapsedSeconds = $clock.Elapsed.TotalSeconds
            stdout = [IO.Path]::GetFileName($stdoutPath)
            stderr = [IO.Path]::GetFileName($stderrPath)
        }
    } finally {
        $stdout.Dispose()
        $stderr.Dispose()
        $process.Dispose()
    }
}

try {
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $PackageRoot 'package-manifest.json') -PathType Leaf)) {
        throw 'PackageRoot must name an extracted AORebirth standalone package.'
    }
    New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
    Write-Host 'PACKAGE_STARTUP_TIMEOUT_SECONDS=1200'
    Write-Host "PACKAGE_STARTUP_LOGS=$logRoot"
    $verification = Invoke-PackageCommand '--verify-startup' 'verify-startup' 1200
    $cleanup = $null
    $exitCode = [int]$verification.actualExitCode
    if ($verification.timedOut) {
        # The launcher owns the installation mutex. Release only our timed-out
        # launcher first, then let the existing controller recover and stop its
        # positively identified services. Never stop processes by name or port.
        $cleanup = Invoke-PackageCommand '--stop-owned' 'stop-owned' 300
        $exitCode = 124
        if ($cleanup.timedOut -or $cleanup.actualExitCode -ne 0) {
            [Console]::Error.WriteLine('PACKAGE_OWNED_CLEANUP=FAILED; inspect retained logs and owned service records.')
        }
    }
    [ordered]@{
        packageRoot = $PackageRoot
        verifierSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
        verification = $verification
        emergencyCleanup = $cleanup
        exitCode = $exitCode
        result = $(if ($verification.timedOut) { 'TIMEOUT' } elseif ($exitCode -eq 0) { 'PASS' } else { 'FAIL' })
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $logRoot 'result.json') -Encoding UTF8
    Write-Host "PACKAGE_STARTUP_EXIT_CODE=$exitCode"
    exit $exitCode
} catch {
    [Console]::Error.WriteLine('PACKAGE_STARTUP_VERIFICATION=FAIL: ' + $_.Exception.Message)
    exit 1
}
