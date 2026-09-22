@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "REPOSITORY_ROOT=%SCRIPT_DIR%.."
set "LEGACY_CHATENGINE=%REPOSITORY_ROOT%\AORebirth\Built\Debug\ChatEngine.exe"
set "CONTRACT_MANIFEST=%SCRIPT_DIR%Tools\Stage5ContractFixtures\LegacyStage5Contracts.manifest"
set "PUBLISH_OUTPUT=%SCRIPT_DIR%artifacts\chatengine\linux-x64\framework-dependent"

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

dotnet run --project Tools\Stage5LinuxContractVerifier\Stage5LinuxContractVerifier.csproj --configuration Release -- "%CONTRACT_MANIFEST%" "%REPOSITORY_ROOT%"
if errorlevel 1 goto :fail

dotnet run --project Tools\Stage5OfflineSmokeTests\Stage5OfflineSmokeTests.csproj --configuration Release -- "%REPOSITORY_ROOT%"
if errorlevel 1 goto :fail

dotnet run --project Tools\PublishDirectoryGuard\PublishDirectoryGuard.csproj -- "%REPOSITORY_ROOT%" linux-x64 framework-dependent
if errorlevel 1 goto :fail

dotnet restore Projects\ChatEngine.Linux.csproj --runtime linux-x64 --nologo
if errorlevel 1 goto :fail

dotnet clean Projects\ChatEngine.Linux.csproj --configuration Release --runtime linux-x64 --nologo
if errorlevel 1 goto :fail

dotnet publish Projects\ChatEngine.Linux.csproj --configuration Release --runtime linux-x64 --self-contained false --nologo -p:PublishTrimmed=false -p:PublishAot=false -p:PublishSingleFile=false --output "%PUBLISH_OUTPUT%"
if errorlevel 1 goto :fail

dotnet run --project Tools\Stage5OfflineSmokeTests\Stage5OfflineSmokeTests.csproj --configuration Release -- "%REPOSITORY_ROOT%" "%PUBLISH_OUTPUT%"
if errorlevel 1 goto :fail

popd
exit /b 0

:fail
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
