@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "PACKAGE_ROOT=%~dp0"
set "DEPLOY=%PACKAGE_ROOT%AORebirthClientPatchDeploy.exe"
set "CLIENT_ROOT=%~1"
if not defined CLIENT_ROOT if exist "%CD%\AnarchyOnline.exe" set "CLIENT_ROOT=%CD%"
if not defined CLIENT_ROOT if exist "C:\Funcom\Anarchy Online\AnarchyOnline.exe" set "CLIENT_ROOT=C:\Funcom\Anarchy Online"
if not defined CLIENT_ROOT if exist "%ProgramFiles(x86)%\Funcom\Anarchy Online\AnarchyOnline.exe" set "CLIENT_ROOT=%ProgramFiles(x86)%\Funcom\Anarchy Online"
if not defined CLIENT_ROOT if exist "%ProgramFiles%\Funcom\Anarchy Online\AnarchyOnline.exe" set "CLIENT_ROOT=%ProgramFiles%\Funcom\Anarchy Online"
if not defined CLIENT_ROOT (
  echo Usage: Uninstall.cmd "C:\path\to\Anarchy Online"
  exit /b 2
)
if not exist "%DEPLOY%" (
  echo ERROR package deployment helper is missing.
  exit /b 1
)

"%DEPLOY%" uninstall "%CLIENT_ROOT%" "%PACKAGE_ROOT%."
exit /b %ERRORLEVEL%
