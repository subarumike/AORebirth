@echo off
setlocal

set MSBUILD=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe

pushd "%~dp0.."
if errorlevel 1 (
    echo [AORebirth Build] Failed to switch to repository root.
    exit /b 1
)

if not exist "%MSBUILD%" (
    echo [AORebirth Build] MSBuild.exe not found: %MSBUILD%
    popd
    exit /b 1
)

echo [AORebirth Build] Cleaning stale build processes...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1

echo [AORebirth Build] Building AORebirth.Core...
"%MSBUILD%" "AORebirth\Libraries\Source\AORebirth.Core\AORebirth.Core.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
set CORE_EXIT=%ERRORLEVEL%
if not "%CORE_EXIT%"=="0" (
    echo [AORebirth Build] AORebirth.Core failed with exit code %CORE_EXIT%.
    popd
    exit /b %CORE_EXIT%
)

echo [AORebirth Build] Cleaning stale build processes before ZoneEngine...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1

echo [AORebirth Build] Building ZoneEngine...
"%MSBUILD%" "AORebirth\Server\ZoneEngine\ZoneEngine.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
set ZONE_EXIT=%ERRORLEVEL%
if not "%ZONE_EXIT%"=="0" (
    echo [AORebirth Build] ZoneEngine failed with exit code %ZONE_EXIT%.
    popd
    exit /b %ZONE_EXIT%
)

echo [AORebirth Build] Build succeeded.
popd
exit /b 0
