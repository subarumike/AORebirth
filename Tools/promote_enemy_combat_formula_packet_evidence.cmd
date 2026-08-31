@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
cd /d "%~dp0.."
%AO_REBIRTH_PYTHON% Tools\promote_enemy_combat_formula_packet_evidence.py %*
exit /b %errorlevel%
