@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "ROOT=%~dp0.."
pushd "%ROOT%" >nul

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [Database Preflight Tests] FAIL: vswhere.exe was not found.
    popd >nul
    exit /b 1
)

set "MSBUILD="
for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -find MSBuild\Current\Bin\MSBuild.exe`) do (
    if not defined MSBUILD set "MSBUILD=%%I"
)

if not defined MSBUILD (
    echo [Database Preflight Tests] FAIL: MSBuild.exe was not found.
    popd >nul
    exit /b 1
)

"%MSBUILD%" "Tools\DatabasePreflight\DatabasePreflight.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 (
    echo [Database Preflight Tests] FAIL: build failed.
    popd >nul
    exit /b 1
)

call preflight-database.cmd --self-test
if errorlevel 1 (
    echo [Database Preflight Tests] FAIL: deterministic self-test failed.
    popd >nul
    exit /b 1
)

set "AO_REBIRTH_MYSQL_CONNECTION="
call preflight-database.cmd >nul
set "MISSING_EXIT=%ERRORLEVEL%"
if not "%MISSING_EXIT%"=="10" (
    echo [Database Preflight Tests] FAIL: missing-environment contract returned %MISSING_EXIT% instead of 10.
    popd >nul
    exit /b 1
)

echo [Database Preflight Tests] PASS.
popd >nul
exit /b 0
