@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%" || exit /b 1

dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet build AORebirth.Linux.slnx --configuration Release --nologo
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" (
    popd
    exit /b %EXIT_CODE%
)

dotnet run --project Tools\CompatibilitySmokeTests\CompatibilitySmokeTests.csproj --configuration Release --no-build
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
