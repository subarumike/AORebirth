@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "TEST_PROJECT=%SCRIPT_DIR%Tools\Stage6MySqlIntegrationTests\Stage6MySqlIntegrationTests.csproj"
set "TEST_MODE=--validate-offline"

if "%~1"=="" goto :arguments_ready
if not "%~1"=="--run-disposable" goto :usage
if not "%~2"=="" goto :usage
set "TEST_MODE=--run-disposable"

:arguments_ready
pushd "%SCRIPT_DIR%" || exit /b 1

dotnet restore "%TEST_PROJECT%" --nologo
if errorlevel 1 goto :fail

dotnet build "%TEST_PROJECT%" --configuration Release --no-restore --nologo
if errorlevel 1 goto :fail

dotnet run --project "%TEST_PROJECT%" --configuration Release --no-build -- %TEST_MODE%
if errorlevel 1 goto :fail

popd
exit /b 0

:usage
echo Usage: verify-stage6-mysql.cmd [--run-disposable]
echo The disposable run also requires AO_REBIRTH_CONFIG_PATH, AO_REBIRTH_MYSQL_CONNECTION,
echo and AO_REBIRTH_STAGE6_DISPOSABLE_ACK=AO_REBIRTH_STAGE6_DISPOSABLE_ONLY.
exit /b 2

:fail
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
