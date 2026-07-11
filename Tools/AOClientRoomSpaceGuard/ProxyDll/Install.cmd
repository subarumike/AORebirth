@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "PACKAGE_ROOT=%~dp0"
set "DEPLOY=%PACKAGE_ROOT%AORoomSpaceFixDeploy.exe"
set "CLIENT_ROOT=%~1"
if not defined CLIENT_ROOT if exist "%CD%\AnarchyOnline.exe" set "CLIENT_ROOT=%CD%"
if not defined CLIENT_ROOT (
  echo Usage: Install.cmd "C:\path\to\Anarchy Online"
  exit /b 2
)
if not exist "%DEPLOY%" (
  echo ERROR package deployment helper is missing.
  exit /b 1
)

"%DEPLOY%" install "%CLIENT_ROOT%" "%PACKAGE_ROOT%."
exit /b %ERRORLEVEL%
