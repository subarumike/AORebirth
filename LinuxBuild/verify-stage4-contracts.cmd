@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "LEGACY_COMMUNICATION=%SCRIPT_DIR%..\AORebirth\Libraries\Source\AORebirth.Communication\bin\Debug\AORebirth.Communication.dll"
set "CONTRACT_MANIFEST=%SCRIPT_DIR%Tools\CompatibilitySmokeTests\Fixtures\LegacyStage4Contracts.manifest"

if not exist "%LEGACY_COMMUNICATION%" (
    echo ERROR: legacy AORebirth.Communication is missing; run tools\build_aorebirth_debug.cmd first.
    exit /b 1
)

if not exist "%CONTRACT_MANIFEST%" (
    echo ERROR: Stage 4 legacy contract manifest is missing.
    exit /b 1
)

pushd "%SCRIPT_DIR%" || exit /b 1
dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet build Tools\LegacyStage4ContractTool\LegacyStage4ContractTool.csproj --configuration Release --nologo
if errorlevel 1 (
    popd
    exit /b 1
)

Tools\LegacyStage4ContractTool\bin\Release\net48\LegacyStage4ContractTool.exe verify "%CONTRACT_MANIFEST%"
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet run --project Tools\Stage4LinuxContractVerifier\Stage4LinuxContractVerifier.csproj --configuration Release -- "%CONTRACT_MANIFEST%"
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
