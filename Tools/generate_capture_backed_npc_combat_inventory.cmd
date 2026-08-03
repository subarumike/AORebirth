@echo off
setlocal

cd /d "%~dp0.."
if "%~1"=="" goto :check
if /I "%~1"=="--self-test" goto :self_test
python Tools\generated_combat_pipeline.py %*
exit /b %errorlevel%

:check
python Tools\generated_combat_pipeline.py --check
exit /b %errorlevel%

:self_test
python tools-temp\AOSharpCaptureAnalyzer\extract_capture_backed_npc_combat.py --self-test
exit /b %errorlevel%
