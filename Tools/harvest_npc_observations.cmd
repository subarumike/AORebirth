@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

MSBuild.exe tools-temp\AOSharpCaptureAnalyzer\AOSharpCaptureAnalyzer.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 goto :fail

tools-temp\AOSharpCaptureAnalyzer\bin\Debug\AOSharpCaptureAnalyzer.exe --self-test
if errorlevel 1 goto :fail

python -m unittest Tools.tests.test_npc_observation_harvester
if errorlevel 1 goto :fail

python Tools\npc_observation_harvester.py %*
if errorlevel 1 goto :fail

popd
exit /b 0

:fail
set "NPC_HARVEST_EXIT=%errorlevel%"
popd
exit /b %NPC_HARVEST_EXIT%
