@echo off
setlocal
rem Recreate Windows PowerShell module paths instead of inheriting PowerShell 7 modules.
set "PSModulePath="
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-package.ps1" %*
exit /b %errorlevel%
