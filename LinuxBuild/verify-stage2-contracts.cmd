@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "LEGACY_ENUMS=%SCRIPT_DIR%..\AORebirth\Libraries\Source\AORebirth.Enums\bin\Debug\AORebirth.Enums.dll"
set "CONTRACT_MANIFEST=%SCRIPT_DIR%Tools\CompatibilitySmokeTests\Fixtures\LegacyStage2PublicContracts.manifest"

if not exist "%LEGACY_ENUMS%" (
    echo ERROR: legacy Stage 2 assemblies are missing; run tools\build_aorebirth_debug.cmd first.
    exit /b 1
)

pushd "%SCRIPT_DIR%" || exit /b 1
dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet build Tools\LegacyStage2ContractTool\LegacyStage2ContractTool.csproj --configuration Release --nologo
if errorlevel 1 (
    popd
    exit /b 1
)

Tools\LegacyStage2ContractTool\bin\Release\net48\LegacyStage2ContractTool.exe verify "%CONTRACT_MANIFEST%"
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet run --project Tools\CompatibilitySmokeTests\CompatibilitySmokeTests.csproj --configuration Release
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
