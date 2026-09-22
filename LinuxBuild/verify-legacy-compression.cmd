@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "LEGACY_UTILITY=%SCRIPT_DIR%..\AORebirth\Libraries\Source\Utility\bin\Debug\Utility.dll"
set "FIXTURE_DIR=%SCRIPT_DIR%Projects\obj\CompatibilityFixtures\Linux"

if not exist "%LEGACY_UTILITY%" (
    echo ERROR: legacy Utility.dll is missing; run tools\build_aorebirth_debug.cmd first.
    exit /b 1
)

pushd "%SCRIPT_DIR%" || exit /b 1
dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet build Tools\LegacyUtilityFixtureTool\LegacyUtilityFixtureTool.csproj --configuration Release --nologo
if errorlevel 1 (
    popd
    exit /b 1
)

dotnet run --project Tools\CompatibilitySmokeTests\CompatibilitySmokeTests.csproj --configuration Release -- --write-utility-fixtures "%FIXTURE_DIR%"
if errorlevel 1 (
    popd
    exit /b 1
)

Tools\LegacyUtilityFixtureTool\bin\Release\net48\LegacyUtilityFixtureTool.exe verify "%FIXTURE_DIR%"
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
