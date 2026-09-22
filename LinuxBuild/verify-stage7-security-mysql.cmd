@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "TEST_PROJECT=%SCRIPT_DIR%Tools\Stage7MySqlSecurityIntegrationTests\Stage7MySqlSecurityIntegrationTests.csproj"
set "TEST_BINARY=%SCRIPT_DIR%Tools\Stage7MySqlSecurityIntegrationTests\bin\Release\net10.0\Stage7MySqlSecurityIntegrationTests.dll"
set "TEST_MODE="

if "%~1"=="" goto :arguments_ready
if not "%~1"=="--run-disposable" goto :usage
if not "%~2"=="" goto :usage
set "TEST_MODE=--run-disposable"

:arguments_ready
pushd "%SCRIPT_DIR%" || exit /b 1

call :verify_and_build_without_live_environment
if errorlevel 1 goto :fail

if "%TEST_MODE%"=="" (
    call :run_offline_without_live_environment
) else (
    dotnet "%TEST_BINARY%" %TEST_MODE%
)
if errorlevel 1 goto :fail

popd
exit /b 0

:usage
echo Usage: verify-stage7-security-mysql.cmd [--run-disposable]
echo The disposable run also requires AO_REBIRTH_CONFIG_PATH, AO_REBIRTH_MYSQL_CONNECTION,
echo AO_REBIRTH_REQUIRED_SQL_TYPE=MySql, and
echo AO_REBIRTH_STAGE7_SECURITY_DISPOSABLE_ACK=AO_REBIRTH_STAGE7_SECURITY_DISPOSABLE_ONLY.
exit /b 2

:fail
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%

:verify_and_build_without_live_environment
setlocal
set "AO_REBIRTH_MYSQL_CONNECTION="
set "AO_REBIRTH_CONFIG_PATH="
set "AO_REBIRTH_REQUIRED_SQL_TYPE="
set "AO_REBIRTH_STAGE7_SECURITY_DISPOSABLE_ACK="

call "%SCRIPT_DIR%verify-stage7-contracts.cmd"
if errorlevel 1 goto :isolated_fail

dotnet restore "%TEST_PROJECT%" --nologo
if errorlevel 1 goto :isolated_fail

dotnet build "%TEST_PROJECT%" --configuration Release --no-restore --nologo
if errorlevel 1 goto :isolated_fail

if not exist "%TEST_BINARY%" goto :isolated_fail
endlocal
exit /b 0

:run_offline_without_live_environment
setlocal
set "AO_REBIRTH_MYSQL_CONNECTION="
set "AO_REBIRTH_CONFIG_PATH="
set "AO_REBIRTH_REQUIRED_SQL_TYPE="
set "AO_REBIRTH_STAGE7_SECURITY_DISPOSABLE_ACK="
dotnet "%TEST_BINARY%"
if errorlevel 1 goto :isolated_fail
endlocal
exit /b 0

:isolated_fail
endlocal
exit /b 1
