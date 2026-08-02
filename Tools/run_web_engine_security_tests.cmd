@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "ROOT=%~dp0.."
pushd "%ROOT%" >nul
if errorlevel 1 (
    echo [WebEngine Security Tests] FAIL: cannot enter repository root.
    exit /b 1
)

set "HTTP_PROXY=http://127.0.0.1:9"
set "HTTPS_PROXY=http://127.0.0.1:9"
set "ALL_PROXY=http://127.0.0.1:9"
set "NO_PROXY="

python Tools\tests\test_webcore_bootstrap_contracts.py
if errorlevel 1 (
    echo [WebEngine Security Tests] FAIL: WebCore bootstrap source contract failed.
    popd >nul
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

fc /b "AORebirth\Config\WebCoreAssets.manifest.xml" "AORebirth\Built\Debug\WebCoreAssets.manifest.xml" >nul
if errorlevel 1 (
    echo [WebEngine Security Tests] FAIL: runtime WebCore manifest differs from the checked-in authority.
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

pushd "AORebirth\Built\Debug" >nul
WebEngine.exe /validate-webcore-manifest
set "WEBCORE_MANIFEST_EXIT=%ERRORLEVEL%"
if not "%WEBCORE_MANIFEST_EXIT%"=="0" (
    echo [WebEngine Security Tests] FAIL: checked-in WebCore manifest authority failed production parsing.
    popd >nul
    popd >nul
    exit /b 1
)

WebEngine.exe /self-test-webcore-assets
set "WEBCORE_SELF_TEST_EXIT=%ERRORLEVEL%"
popd >nul
if not "%WEBCORE_SELF_TEST_EXIT%"=="0" (
    echo [WebEngine Security Tests] FAIL: WebCore asset self-test failed.
    popd >nul
    exit /b 1
)

echo [WebEngine Security Tests] PASS.
popd >nul
exit /b 0
