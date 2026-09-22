@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "LEGACY_DATABASE=%SCRIPT_DIR%..\AORebirth\Libraries\Source\AORebirth.Database\bin\Debug\AORebirth.Database.dll"
set "LEGACY_STATS=%SCRIPT_DIR%..\AORebirth\Libraries\Source\AORebirth.Stats\bin\Debug\AORebirth.Stats.dll"
set "CONTRACT_MANIFEST=%SCRIPT_DIR%Tools\CompatibilitySmokeTests\Fixtures\LegacyStage3Contracts.manifest"

if not exist "%LEGACY_DATABASE%" (
    echo ERROR: legacy AORebirth.Database is missing; run tools\build_aorebirth_debug.cmd first.
    exit /b 1
)

if not exist "%LEGACY_STATS%" (
    echo ERROR: legacy AORebirth.Stats is missing; run tools\build_aorebirth_debug.cmd first.
    exit /b 1
)

if not exist "%CONTRACT_MANIFEST%" (
    echo ERROR: Stage 3 legacy contract manifest is missing.
    exit /b 1
)

pushd "%SCRIPT_DIR%" || exit /b 1
dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet build Tools\LegacyStage3ContractTool\LegacyStage3ContractTool.csproj --configuration Release --nologo
if errorlevel 1 (
    popd
    exit /b 1
)

Tools\LegacyStage3ContractTool\bin\Release\net48\LegacyStage3ContractTool.exe verify "%CONTRACT_MANIFEST%"
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet run --project Tools\Stage3LinuxContractVerifier\Stage3LinuxContractVerifier.csproj --configuration Release -- "%CONTRACT_MANIFEST%"
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
