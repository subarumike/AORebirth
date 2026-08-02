@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "ROOT=%~dp0.."
pushd "%ROOT%" >nul
if errorlevel 1 (
    echo [WebEngine Security Tests] FAIL: cannot enter repository root.
    exit /b 1
)

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [WebEngine Security Tests] FAIL: vswhere.exe was not found.
    popd >nul
    exit /b 1
)

set "MSBUILD="
for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -find MSBuild\Current\Bin\MSBuild.exe`) do (
    if not defined MSBUILD set "MSBUILD=%%I"
)

if not defined MSBUILD (
    echo [WebEngine Security Tests] FAIL: MSBuild.exe was not found.
    popd >nul
    exit /b 1
)

"%MSBUILD%" "AORebirth\Server\WebEngine\WebEngine.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 (
    echo [WebEngine Security Tests] FAIL: WebEngine build failed.
    popd >nul
    exit /b 1
)

pushd "AORebirth\Built\Debug" >nul
WebEngine.exe /self-test-php-runtime
set "SELF_TEST_EXIT=%ERRORLEVEL%"
popd >nul
if not "%SELF_TEST_EXIT%"=="0" (
    echo [WebEngine Security Tests] FAIL: PHP runtime self-test failed.
    popd >nul
    exit /b 1
)

echo [WebEngine Security Tests] PASS.
popd >nul
exit /b 0
