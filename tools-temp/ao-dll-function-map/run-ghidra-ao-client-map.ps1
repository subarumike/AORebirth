param(
    [string]$AoDir = "C:\Funcom\Anarchy Online",
    [string]$MapDir = "C:\Users\Mike\Documents\Cellao-Clean\CellAO\Documentation\AOClientDllFunctionMap",
    [string]$WorkDir = "C:\Users\Mike\Documents\Cellao-Clean\tools-temp\ao-dll-function-map\ghidra-work",
    [string]$AnalyzeHeadless = "C:\Users\Mike\Downloads\ghidra-master\installed\ghidra_12.2_DEV_20260428_win_x86_64\ghidra_12.2_DEV\support\analyzeHeadless.bat",
    [int]$TimeoutPerFileSeconds = 240
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$scriptPath = Join-Path $scriptRoot "ExportAoFunctions.java"
$summaryPath = Join-Path $MapDir "ao_client_dll_summary.csv"
$stagingDir = Join-Path $WorkDir "ao-client-dlls"
$projectDir = Join-Path $WorkDir "project"
$ghidraOutDir = Join-Path $MapDir "ghidra"
$combinedAllPath = Join-Path $ghidraOutDir "ao_client_dll_ghidra_functions_all.csv"
$combinedReadablePath = Join-Path $ghidraOutDir "ao_client_dll_ghidra_functions_readable.csv"
$runLogPath = Join-Path $ghidraOutDir "ghidra_headless_run.log"
$scriptLogPath = Join-Path $ghidraOutDir "ghidra_script.log"

if (-not (Test-Path -LiteralPath $AnalyzeHeadless)) {
    throw "analyzeHeadless not found: $AnalyzeHeadless"
}

if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Ghidra script not found: $scriptPath"
}

if (-not (Test-Path -LiteralPath $summaryPath)) {
    throw "DLL summary not found. Run generate-ao-client-dll-map.ps1 first: $summaryPath"
}

New-Item -ItemType Directory -Force -Path $stagingDir, $projectDir, $ghidraOutDir | Out-Null
Get-ChildItem -LiteralPath $stagingDir -File -Filter "*.dll" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -LiteralPath $ghidraOutDir -File -Filter "*.ghidra_functions.csv" -ErrorAction SilentlyContinue | Remove-Item -Force

$aoDlls = Import-Csv -Path $summaryPath |
    Where-Object { $_.Category -eq "AOClient" } |
    Sort-Object Dll

foreach ($row in $aoDlls) {
    $source = Join-Path $AoDir $row.Dll
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $stagingDir $row.Dll) -Force
    }
}

Write-Host "Running Ghidra over $($aoDlls.Count) AO client DLLs."

$args = @(
    $projectDir,
    "AOClientDllFunctionMap",
    "-import", $stagingDir,
    "-recursive",
    "-overwrite",
    "-scriptPath", $scriptRoot,
    "-postScript", "ExportAoFunctions.java", $ghidraOutDir,
    "-analysisTimeoutPerFile", $TimeoutPerFileSeconds,
    "-log", $runLogPath,
    "-scriptlog", $scriptLogPath,
    "-max-cpu", "4",
    "-deleteProject",
    "-okToDelete"
)

& $AnalyzeHeadless @args
if ($LASTEXITCODE -ne 0) {
    throw "Ghidra analyzeHeadless failed with exit code $LASTEXITCODE"
}

$functionFiles = Get-ChildItem -LiteralPath $ghidraOutDir -File -Filter "*.ghidra_functions.csv" | Sort-Object Name
if ($functionFiles.Count -eq 0) {
    throw "Ghidra produced no function CSV files in $ghidraOutDir"
}

$first = $true
foreach ($file in $functionFiles) {
    if ($first) {
        Get-Content -LiteralPath $file.FullName | Set-Content -Encoding UTF8 -Path $combinedAllPath
        $first = $false
    }
    else {
        Get-Content -LiteralPath $file.FullName | Select-Object -Skip 1 | Add-Content -Encoding UTF8 -Path $combinedAllPath
    }
}

$allFunctions = Import-Csv -Path $combinedAllPath
$allFunctions |
    Where-Object { $_.ReadableName -eq "true" } |
    Export-Csv -NoTypeInformation -Encoding UTF8 -Path $combinedReadablePath

$ghidraSummaryPath = Join-Path $ghidraOutDir "ao_client_dll_ghidra_summary.csv"
$allFunctions |
    Group-Object Program |
    ForEach-Object {
        $rows = $_.Group
        [pscustomobject]@{
            Program = $_.Name
            FunctionCount = $rows.Count
            ReadableFunctionCount = @($rows | Where-Object { $_.ReadableName -eq "true" }).Count
            ExternalFunctionCount = @($rows | Where-Object { $_.IsExternal -eq "true" }).Count
            ThunkFunctionCount = @($rows | Where-Object { $_.IsThunk -eq "true" }).Count
        }
    } |
    Sort-Object Program |
    Export-Csv -NoTypeInformation -Encoding UTF8 -Path $ghidraSummaryPath

Write-Host "Done."
Write-Host "Ghidra per-DLL CSVs: $ghidraOutDir"
Write-Host "Combined all functions: $combinedAllPath"
Write-Host "Combined readable functions: $combinedReadablePath"
Write-Host "Summary: $ghidraSummaryPath"
