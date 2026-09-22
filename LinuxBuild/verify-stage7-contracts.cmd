@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "REPOSITORY_ROOT=%SCRIPT_DIR%.."
set "LEGACY_LOGINENGINE=%REPOSITORY_ROOT%\AORebirth\Built\Debug\LoginEngine.exe"
set "CONTRACT_MANIFEST=%SCRIPT_DIR%Tools\Stage7ContractFixtures\LegacyStage7Contracts.manifest"
set "PUBLISH_OUTPUT=%SCRIPT_DIR%artifacts\loginengine\linux-x64\framework-dependent"

if not exist "%LEGACY_LOGINENGINE%" (
    echo ERROR: legacy LoginEngine is missing; build AORebirth\Server\LoginEngine\LoginEngine.csproj in Debug first.
    exit /b 1
)

if not exist "%CONTRACT_MANIFEST%" (
    echo ERROR: Stage 7 legacy contract manifest is missing.
    exit /b 1
)

pushd "%SCRIPT_DIR%" || exit /b 1

dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 goto :fail

dotnet build Tools\LegacyStage7ContractTool\LegacyStage7ContractTool.csproj --configuration Release --nologo
if errorlevel 1 goto :fail

Tools\LegacyStage7ContractTool\bin\Release\net48\LegacyStage7ContractTool.exe verify "%CONTRACT_MANIFEST%" "%LEGACY_LOGINENGINE%"
if errorlevel 1 goto :fail

dotnet run --project Tools\Stage7LinuxContractVerifier\Stage7LinuxContractVerifier.csproj --configuration Release -- "%CONTRACT_MANIFEST%" "%REPOSITORY_ROOT%"
if errorlevel 1 goto :fail

dotnet run --project Tools\Stage7OfflineSmokeTests\Stage7OfflineSmokeTests.csproj --configuration Release -- "%REPOSITORY_ROOT%"
if errorlevel 1 goto :fail

call publish-loginengine.cmd linux-x64 false
if errorlevel 1 goto :fail

dotnet run --project Tools\Stage7OfflineSmokeTests\Stage7OfflineSmokeTests.csproj --configuration Release -- "%REPOSITORY_ROOT%" "%PUBLISH_OUTPUT%"
if errorlevel 1 goto :fail

popd
exit /b 0

:fail
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
