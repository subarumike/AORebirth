@echo off
setlocal
if not "%~1"=="--engine" goto :usage
if "%~2"=="" goto :usage
if not "%~3"=="--login-engine" goto :usage
if "%~4"=="" goto :usage
if "%~5"=="" goto :run
if not "%~5"=="--runtime-gamedata" goto :usage
if "%~6"=="" goto :usage
if "%~7"=="" goto :isolated
if not "%~7"=="--mission-matrix" goto :usage
if not "%~8"=="" goto :usage
dotnet run --project "%~dp0ZoneEngineSchemaValidation\ZoneEngineSchemaValidation.csproj" --configuration Release -- --run-connected --engine "%~2" --login-engine "%~4" --runtime-gamedata "%~6" --mission-matrix
exit /b %errorlevel%
:isolated
dotnet run --project "%~dp0ZoneEngineSchemaValidation\ZoneEngineSchemaValidation.csproj" --configuration Release -- --run-connected --engine "%~2" --login-engine "%~4" --runtime-gamedata "%~6"
exit /b %errorlevel%
:run
dotnet run --project "%~dp0ZoneEngineSchemaValidation\ZoneEngineSchemaValidation.csproj" --configuration Release -- --run-connected --engine "%~2" --login-engine "%~4"
exit /b %errorlevel%
:usage
echo Usage: run_newengine_connected_acceptance.cmd --engine ABSOLUTE_ZONEENGINE_NEW_DLL --login-engine ABSOLUTE_LOGINENGINE_EXE_OR_DLL [--runtime-gamedata NORMALIZED_GAMEDATA_DIRECTORY [--mission-matrix]]
exit /b 64
