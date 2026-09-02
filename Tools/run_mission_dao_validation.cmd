@echo off
setlocal
set "AO_REBIRTH_ALLOW_DISPOSABLE_MISSION_DAO_VALIDATION=1"
dotnet run --project "%~dp0MissionDaoValidation\MissionDaoValidation.csproj" --configuration Release -- --run-disposable
exit /b %errorlevel%
