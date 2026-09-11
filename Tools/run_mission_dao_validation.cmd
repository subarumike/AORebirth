@echo off
setlocal
if "%~1"=="--isolated-sources" goto isolated
if not "%~1"=="" exit /b 2
set "AO_REBIRTH_ALLOW_DISPOSABLE_MISSION_DAO_VALIDATION=1"
dotnet run --project "%~dp0MissionDaoValidation\MissionDaoValidation.csproj" --configuration Release -- --run-disposable
exit /b %errorlevel%

:isolated
if not "%~2"=="" exit /b 2
set "AO_REBIRTH_ALLOW_DISPOSABLE_MISSION_DAO_VALIDATION=1"
dotnet run --project "%~dp0MissionDaoValidation\MissionDaoValidation.csproj" --configuration Release -p:IsolatedMissionSources=true -- --run-disposable
exit /b %errorlevel%
