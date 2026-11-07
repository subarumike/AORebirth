@echo off
setlocal
rem Prefer Windows PowerShell's own standard modules over a PowerShell 7 parent.
set "PSModulePath=%SystemRoot%\System32\WindowsPowerShell\v1.0\Modules;%ProgramFiles%\WindowsPowerShell\Modules"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-package.ps1" %*
exit /b %errorlevel%
