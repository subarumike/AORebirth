@echo off
setlocal
cd /d "%~dp0.."
python Tools\scan_secrets.py
exit /b %errorlevel%
