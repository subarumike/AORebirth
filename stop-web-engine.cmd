@echo off
setlocal EnableExtensions
powershell -NoProfile -File "%~dp0stop-engines.ps1" -EngineName WebEngine
exit /b %ERRORLEVEL%
