@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "REPOSITORY_ROOT=%SCRIPT_DIR%.."
set "LEGACY_CHATENGINE=%REPOSITORY_ROOT%\AORebirth\Built\Debug\ChatEngine.exe"
set "CONTRACT_MANIFEST=%SCRIPT_DIR%Tools\Stage5ContractFixtures\LegacyStage5Contracts.manifest"

if not exist "%LEGACY_CHATENGINE%" (
    echo ERROR: legacy ChatEngine is missing; build AORebirth\Server\ChatEngine\ChatEngine.csproj in Debug first.
    exit /b 1
)

if not exist "%CONTRACT_MANIFEST%" (
    echo ERROR: Stage 5 legacy contract manifest is missing.
    exit /b 1
)

pushd "%SCRIPT_DIR%" || exit /b 1

dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 goto :fail

dotnet build Tools\LegacyStage5ContractTool\LegacyStage5ContractTool.csproj --configuration Release --nologo
if errorlevel 1 goto :fail

Tools\LegacyStage5ContractTool\bin\Release\net48\LegacyStage5ContractTool.exe verify "%CONTRACT_MANIFEST%" "%LEGACY_CHATENGINE%"
if errorlevel 1 goto :fail

popd
exit /b 0

:fail
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
