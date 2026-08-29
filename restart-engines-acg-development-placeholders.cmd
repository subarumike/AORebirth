@echo off
setlocal EnableExtensions

if "%~1"=="" goto :usage
if "%~2"=="" goto :usage

set "ACG_MODE=%~1"
if /i "%ACG_MODE%"=="CapturePlan" goto :mode_ok
if /i "%ACG_MODE%"=="CurrentPlayfieldPrimary" goto :mode_ok
if /i "%ACG_MODE%"=="CurrentPlayfieldAllPoints" goto :mode_ok
if /i "%ACG_MODE%"=="ResolvedComparison" goto :mode_ok
echo [AORebirth ACG Placeholders] Unsupported mode: %ACG_MODE%
goto :usage

:mode_ok
set "AO_REBIRTH_ACG_PLACEHOLDER_MODE=%ACG_MODE%"
set "AO_REBIRTH_ACG_PLACEHOLDER_PLAYFIELD=%~2"
if /i "%ACG_MODE%"=="CurrentPlayfieldAllPoints" (
    echo [AORebirth ACG Placeholders] WARNING: AdditionalPoints runtime multiplicity semantics are unresolved.
)
call "%~dp0restart-engines.cmd"
exit /b %ERRORLEVEL%

:usage
echo Usage: %~nx0 ^<CapturePlan^|CurrentPlayfieldPrimary^|CurrentPlayfieldAllPoints^|ResolvedComparison^> ^<playfield-resource-instance^>
exit /b 2
