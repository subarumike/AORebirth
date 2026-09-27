@echo off
setlocal
if "%~1"=="" exit /b 64
if not exist "%~1\Missions\MissionOffers.json" exit /b 65
if exist "%~1\Missions\RollBodies.json" exit /b 66
if exist "%~1\Missions\RollTemplate.json" exit /b 66
set "AO_REBIRTH_GAMEDATA_PATH=%~f1"
dotnet test "%~dp0..\AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj" --logger trx --results-directory "%~dp0..\build-verify\normalized-mission-tests"
exit /b %errorlevel%
