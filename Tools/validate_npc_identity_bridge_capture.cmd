@echo off
setlocal
cd /d "%~dp0.."

python -m unittest Tools.tests.test_npc_identity_bridge_capture_contract Tools.tests.test_npc_identity_bridge_replay Tools.tests.test_npc_identity_bridge_resolver
if errorlevel 1 goto fail

python -m unittest Tools.tests.test_npc_observation_harvester
if errorlevel 1 goto fail

python -m unittest Tools.tests.test_npc_placement_identity_resolver
if errorlevel 1 goto fail

MSBuild.exe tools-temp\AOSharpLiveCapture\AOSharpLiveCapture.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 goto fail

MSBuild.exe tools-temp\AOSharpCaptureAnalyzer\AOSharpCaptureAnalyzer.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 goto fail

tools-temp\AOSharpCaptureAnalyzer\bin\Debug\AOSharpCaptureAnalyzer.exe --self-test
if errorlevel 1 goto fail

cmd /d /c tools-temp\start-aosharp-live-capture.cmd --help >nul
if errorlevel 1 goto fail

echo NPC_IDENTITY_BRIDGE_ACCEPTANCE=PASS
exit /b 0

:fail
echo NPC_IDENTITY_BRIDGE_ACCEPTANCE=FAIL
exit /b 1
