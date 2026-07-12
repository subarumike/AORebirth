@echo off
setlocal
python "%~dp0weapon_damage_evidence.py" %*
exit /b %ERRORLEVEL%
