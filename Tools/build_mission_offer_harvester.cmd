@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1

cd /d "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\generate_mission_harvester_ql_table.py --check
if errorlevel 1 exit /b 1

set "HARVESTER_BUILD_ROOT=%CD%\tools-temp\AOSharpMissionOfferHarvesterBuild"
%AO_REBIRTH_PYTHON% Tools\prepare_mission_offer_harvester_build.py "%HARVESTER_BUILD_ROOT%\sdk"
if errorlevel 1 exit /b 1

cmd /d /c MSBuild.exe Tools\AOSharpMissionOfferHarvester\AOSharpMissionOfferHarvester.csproj /t:Build /p:Configuration=Release /p:AOSharpSdkDir="%HARVESTER_BUILD_ROOT%\sdk\lib\net48" /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b 1

call Tools\test_mission_offer_harvester.cmd
if errorlevel 1 exit /b 1

echo MISSION_OFFER_HARVESTER_BUILD=PASS
exit /b 0
