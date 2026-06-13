param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepoRoot {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Test-ExpectedPath {
    param(
        [string]$Name,
        [string]$Path,
        [ValidateSet("Leaf", "Container")]
        [string]$PathType,
        [System.Collections.Generic.List[string]]$Failures
    )

    $exists = Test-Path -LiteralPath $Path -PathType $PathType
    if ($exists) {
        Write-Host "OK      $Name => $Path"
    } else {
        Write-Host "MISSING $Name => $Path"
        $Failures.Add("$Name missing: $Path")
    }
}

$RepoRoot = Resolve-RepoRoot $RepoRoot
$failures = [System.Collections.Generic.List[string]]::new()

$injectorExe = Join-Path $RepoRoot "tools-temp\AOSharpLiveInjector\bin\Debug\AOSharpLiveInjector.exe"
$captureDll = Join-Path $RepoRoot "tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"
$captureOutputRoot = Join-Path $RepoRoot "tools-temp\AOSharpLiveCapture\bin\Debug\captures"
$verificationScript = Join-Path $RepoRoot "tools-temp\current-client-data-verification\Invoke-CurrentClientDataVerification.ps1"
$collectorRoot = Join-Path $RepoRoot "tools-temp\live-data-collector"

Write-Host "Live capture tool path validation"
Write-Host "RepoRoot=$RepoRoot"

Test-ExpectedPath "AOSharpLiveInjector exe" $injectorExe "Leaf" $failures
Test-ExpectedPath "AOSharpLiveCapture dll" $captureDll "Leaf" $failures
Test-ExpectedPath "Capture output root" $captureOutputRoot "Container" $failures
Test-ExpectedPath "Current-client verification script" $verificationScript "Leaf" $failures
Test-ExpectedPath "Live-data-collector root" $collectorRoot "Container" $failures

$liveDataCollectorScripts = @(
    "Start-LiveDataCapture.ps1",
    "Stop-LiveDataCapture.ps1",
    "Get-LiveDataCaptureStatus.ps1",
    "Decode-LiveDataCapture.ps1",
    "Test-LiveDataCollector.ps1",
    "Decode-LiveObservationCapture.py",
    "Decode-LiveS2CFrames.py",
    "Export-LivePacketCoverage.py",
    "Export-LiveCombatLootTimeline.py"
)

foreach ($script in $liveDataCollectorScripts) {
    Test-ExpectedPath "live-data-collector script $script" (Join-Path $collectorRoot $script) "Leaf" $failures
}

$activeScripts = @(
    (Join-Path $RepoRoot "tools-temp\start-local-ao-capture.ps1"),
    (Join-Path $RepoRoot "tools-temp\watch-and-start-live-capture.ps1"),
    (Join-Path $RepoRoot "tools-temp\decode-ao-pcap.ps1"),
    $verificationScript
)
$activeScripts += Get-ChildItem -LiteralPath $collectorRoot -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -in @(
            "Start-LiveDataCapture.ps1",
            "Stop-LiveDataCapture.ps1",
            "Get-LiveDataCaptureStatus.ps1",
            "Decode-LiveDataCapture.ps1",
            "Test-LiveDataCollector.ps1",
            "Decode-LiveS2CFrames.py",
            "Export-LivePacketCoverage.py"
        )
    } |
    Select-Object -ExpandProperty FullName

$staleRepoName = "Cellao" + "-Clean"
foreach ($scriptPath in ($activeScripts | Sort-Object -Unique)) {
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        $failures.Add("Active script missing: $scriptPath")
        continue
    }

    $matches = @(Select-String -LiteralPath $scriptPath -SimpleMatch $staleRepoName -ErrorAction Stop)
    if ($matches.Count -gt 0) {
        foreach ($match in $matches) {
            $failures.Add("Stale repo path reference in $($match.Path):$($match.LineNumber)")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "Validation failed:"
    foreach ($failure in $failures) {
        Write-Host "  $failure"
    }
    exit 1
}

Write-Host "Live capture tool path validation passed."
