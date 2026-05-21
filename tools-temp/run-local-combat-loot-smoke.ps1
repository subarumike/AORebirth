param(
    [string]$Title = "",
    [string]$MobAlias = "beachleet",
    [string]$MobName = "",
    [int]$TargetPid = 0,
    [int]$TimeoutSeconds = 90,
    [switch]$NoBuild,
    [switch]$NoInject,
    [switch]$BasicLoot,
    [switch]$AllowAnyTarget
)

$ErrorActionPreference = "Stop"

$repo = "C:\Users\Mike\Documents\Cellao-Clean"
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
$captureProject = Join-Path $repo "tools-temp\AOSharpLiveCapture\AOSharpLiveCapture.csproj"
$injectorProject = Join-Path $repo "tools-temp\AOSharpLiveInjector\AOSharpLiveInjector.csproj"
$pluginDll = Join-Path $repo "tools-temp\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"
$pluginDir = Split-Path -Parent $pluginDll
$requestFile = Join-Path $pluginDir "run-combat-loot-smoke.txt"
$resultFile = Join-Path $pluginDir "last-combat-loot-smoke.result"
$startCapture = Join-Path $repo "tools-temp\start-local-ao-capture.ps1"
$knownMobNames = @{
    beachleet = "Codex Test Beach Leet"
    leet = "Codex Test Beach Leet"
    islandreet = "Codex Test Island Reet"
    reet = "Codex Test Island Reet"
    shoresnake = "Codex Test Shore Snake"
    snake = "Codex Test Shore Snake"
    rollerrat = "Codex Test Stowaway Rollerrat"
    duneflea = "Codex Test Dune Flea"
    flea = "Codex Test Dune Flea"
    surflizard = "Codex Test Surf Lizard"
    lizard = "Codex Test Surf Lizard"
    cliffmalle = "Codex Test Cliff Malle"
    malle = "Codex Test Cliff Malle"
    reefsalamander = "Codex Test Reef Salamander"
    salamander = "Codex Test Reef Salamander"
    alienspider = "Codex Test Alien Spider - Zix"
    spider = "Codex Test Alien Spider - Zix"
    zix = "Codex Test Alien Spider - Zix"
    a004 = "Beach Leet"
}

if ([string]::IsNullOrWhiteSpace($MobAlias)) {
    $MobAlias = "beachleet"
}

if ([string]::IsNullOrWhiteSpace($MobName) -and $knownMobNames.ContainsKey($MobAlias.ToLowerInvariant())) {
    $MobName = $knownMobNames[$MobAlias.ToLowerInvariant()]
}

if (-not $NoBuild) {
    & $msbuild $captureProject /t:Build /p:Configuration=Debug /m | Write-Host
    & $msbuild $injectorProject /t:Build /p:Configuration=Debug /m | Write-Host
}

if (-not (Test-Path $pluginDll)) {
    throw "Plugin DLL not found: $pluginDll"
}

if ([string]::IsNullOrWhiteSpace($Title) -and -not $AllowAnyTarget) {
    throw "Refusing to run combat loot smoke without -Title. Pass the expected local character title, or use -AllowAnyTarget deliberately."
}

if (-not $NoInject) {
    $target = $null
    if ($TargetPid -gt 0) {
        $target = Get-Process -Id $TargetPid -ErrorAction Stop
        if (-not $AllowAnyTarget) {
            if ($target.MainWindowTitle -notlike "*$Title*") {
                throw "Refusing to attach to pid=$TargetPid title='$($target.MainWindowTitle)' because it does not match -Title '$Title'."
            }
        }
    } else {
        $candidates = Get-Process AnarchyOnline -ErrorAction SilentlyContinue |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_.MainWindowTitle) }

        if (-not [string]::IsNullOrWhiteSpace($Title)) {
            $candidates = $candidates | Where-Object { $_.MainWindowTitle -like "*$Title*" }
        }

        $candidates = @($candidates)
        if ($candidates.Count -eq 0) {
            throw "No running AnarchyOnline process matched. Log into the local server, then rerun this script."
        }

        if ($candidates.Count -gt 1) {
            $list = $candidates | ForEach-Object { "pid=$($_.Id) title=$($_.MainWindowTitle)" }
            throw "More than one AnarchyOnline process matched. Use -TargetPid or -Title. Matches: $($list -join '; ')"
        }

        $target = $candidates[0]
    }

    & $startCapture -TargetPid $target.Id -PluginPath $pluginDll | Write-Host
}

Remove-Item -LiteralPath $resultFile -ErrorAction SilentlyContinue
$requestLines = @("requested={0:o}" -f [DateTime]::UtcNow)
if (-not [string]::IsNullOrWhiteSpace($Title)) {
    $requestLines += "character=$Title"
}
$requestLines += "mobAlias=$MobAlias"
if (-not [string]::IsNullOrWhiteSpace($MobName)) {
    $requestLines += "mobName=$MobName"
}
$requestLines += "requireReopen=$(-not $BasicLoot)"

$requestTempFile = "$requestFile.$PID.tmp"
Set-Content -LiteralPath $requestTempFile -Value $requestLines -Encoding UTF8
Move-Item -LiteralPath $requestTempFile -Destination $requestFile -Force

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
Write-Host "Waiting for combat loot smoke result for up to $TimeoutSeconds seconds..."

while ((Get-Date) -lt $deadline) {
    if (Test-Path $resultFile) {
        $result = Get-Content -Raw -LiteralPath $resultFile
        Write-Host $result

        if ($result -match "RESULT PASS") {
            exit 0
        }

        if ($result -match "RESULT FAIL") {
            exit 2
        }

        exit 3
    }

    Start-Sleep -Milliseconds 500
}

throw "Timed out waiting for combat loot smoke result. Check $pluginDir\smoke-runs for the active log."
