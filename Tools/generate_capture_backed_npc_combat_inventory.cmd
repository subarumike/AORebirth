@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1

cd /d "%~dp0.."
if "%~1"=="" goto :check
if /I "%~1"=="--self-test" goto :self_test
%AO_REBIRTH_PYTHON% Tools\generated_combat_pipeline.py %*
exit /b %errorlevel%

:check
%AO_REBIRTH_PYTHON% Tools\generated_combat_pipeline.py --check
exit /b %errorlevel%

:self_test
%AO_REBIRTH_PYTHON% tools-temp\AOSharpCaptureAnalyzer\extract_capture_backed_npc_combat.py --self-test
exit /b %errorlevel%
