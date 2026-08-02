@echo off
setlocal EnableExtensions
powershell -NoProfile -File "%~dp0stop-engines.ps1" %*
exit /b %ERRORLEVEL%
