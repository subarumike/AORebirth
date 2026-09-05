@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem Build ChatEngine + LoginEngine (net48 packages.config) and ZoneEngine_New (net10.0).
rem Repo root is the parent of this folder. Run from anywhere.

set "REPO=%~dp0.."
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "MSBUILD="
set "CONFIG=Debug"
set "CHAT_PROJ=AORebirth\Server\ChatEngine\ChatEngine.csproj"
set "LOGIN_PROJ=AORebirth\Server\LoginEngine\LoginEngine.csproj"
set "ZONE_PROJ=AORebirth\Server\ZoneEngine_New\ZoneEngine_New.csproj"
set "SLN=AORebirth\AORebirth.sln"
set "CHAT_EXE=AORebirth\Built\Debug\ChatEngine.exe"
set "LOGIN_EXE=AORebirth\Built\Debug\LoginEngine.exe"
set "ZONE_EXE=AORebirth\Built\Debug\ZoneEngine_New\ZoneEngine_New.exe"

cd /d "%REPO%"
if errorlevel 1 (
    echo [NewZoneEngineBuild] Failed to switch to repo: %REPO%
    exit /b 1
)

if not exist "%VSWHERE%" (
    echo [NewZoneEngineBuild] vswhere.exe not found. Install Visual Studio with MSBuild.
    exit /b 1
)

for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
    if not defined MSBUILD set "MSBUILD=%%I"
)
if not defined MSBUILD (
    for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -find MSBuild\Current\Bin\MSBuild.exe`) do (
        if not defined MSBUILD set "MSBUILD=%%I"
    )
)
if not defined MSBUILD (
    echo [NewZoneEngineBuild] MSBuild.exe not found.
    exit /b 1
)

echo [NewZoneEngineBuild] Using MSBuild: %MSBUILD%
echo [NewZoneEngineBuild] Repo: %CD%
echo.

echo [1/4] Cleaning stale build processes...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1
taskkill /F /T /IM NuGet.exe >nul 2>&1

echo.
echo [2/4] Restoring NuGet packages...
call :RestorePackagesConfig
if errorlevel 1 exit /b 1

echo [NewZoneEngineBuild] Restoring ZoneEngine_New PackageReferences (net10.0)...
"%MSBUILD%" "%ZONE_PROJ%" /t:Restore /m:1 /nr:false /v:minimal
if errorlevel 1 (
    echo [NewZoneEngineBuild] ZoneEngine_New restore failed.
    exit /b 1
)

echo.
echo [3/4] Building ChatEngine, LoginEngine, ZoneEngine_New (%CONFIG%)...
"%MSBUILD%" "%CHAT_PROJ%" /t:Build /p:Configuration=%CONFIG% /m:1 /nr:false /v:minimal
if errorlevel 1 (
    echo [NewZoneEngineBuild] ChatEngine build failed.
    exit /b 1
)

"%MSBUILD%" "%LOGIN_PROJ%" /t:Build /p:Configuration=%CONFIG% /m:1 /nr:false /v:minimal
if errorlevel 1 (
    echo [NewZoneEngineBuild] LoginEngine build failed.
    exit /b 1
)

"%MSBUILD%" "%ZONE_PROJ%" /t:Build /p:Configuration=%CONFIG% /m:1 /nr:false /v:minimal
if errorlevel 1 (
    echo [NewZoneEngineBuild] ZoneEngine_New build failed.
    exit /b 1
)

echo.
echo [4/4] Verifying outputs...
if not exist "%CHAT_EXE%" (
    echo [NewZoneEngineBuild] Missing: %CD%\%CHAT_EXE%
    exit /b 1
)
if not exist "%LOGIN_EXE%" (
    echo [NewZoneEngineBuild] Missing: %CD%\%LOGIN_EXE%
    exit /b 1
)
if not exist "%ZONE_EXE%" (
    echo [NewZoneEngineBuild] Missing: %CD%\%ZONE_EXE%
    exit /b 1
)

echo [NewZoneEngineBuild] ChatEngine:     %CD%\%CHAT_EXE%
echo [NewZoneEngineBuild] LoginEngine:    %CD%\%LOGIN_EXE%
echo [NewZoneEngineBuild] ZoneEngine_New: %CD%\%ZONE_EXE%
echo [NewZoneEngineBuild] Done.
exit /b 0

:RestorePackagesConfig
set MISSING_PACKAGES=0
call :CheckPackageConfig "AORebirth\Libraries\Source\AORebirth.Core\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\AORebirth.Database\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\AORebirth.Interfaces\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\AORebirth.Communication\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\Cell.Core\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\Exceptions\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\Utility\packages.config"
call :CheckPackageConfig "AORebirth\Server\ChatEngine\packages.config"
call :CheckPackageConfig "AORebirth\Server\LoginEngine\packages.config"

if "%MISSING_PACKAGES%"=="0" (
    echo [NewZoneEngineBuild] packages.config folders already present; skipping solution restore.
    exit /b 0
)

echo [NewZoneEngineBuild] Missing packages.config folders; restoring %SLN%...
"%MSBUILD%" "%SLN%" /t:Restore /p:RestorePackagesConfig=true /m:1 /nr:false /v:minimal
if errorlevel 1 (
    echo [NewZoneEngineBuild] Solution packages.config restore failed.
    exit /b 1
)
echo [NewZoneEngineBuild] Solution restore completed.
exit /b 0

:CheckPackageConfig
set "PACKAGE_CONFIG=%~1"
if not exist "%PACKAGE_CONFIG%" (
    echo [NewZoneEngineBuild] Missing package config: %PACKAGE_CONFIG%
    set MISSING_PACKAGES=1
    exit /b 0
)
for /F "tokens=2,3" %%A in ('findstr /I /C:"<package " "%PACKAGE_CONFIG%"') do (
    set PACKAGE_ID=%%A
    set PACKAGE_VERSION=%%B
    set PACKAGE_ID=!PACKAGE_ID:id=!
    set PACKAGE_ID=!PACKAGE_ID:"=!
    if "!PACKAGE_ID:~0,1!"=="=" set PACKAGE_ID=!PACKAGE_ID:~1!
    set PACKAGE_VERSION=!PACKAGE_VERSION:version=!
    set PACKAGE_VERSION=!PACKAGE_VERSION:"=!
    if "!PACKAGE_VERSION:~0,1!"=="=" set PACKAGE_VERSION=!PACKAGE_VERSION:~1!
    if not exist "AORebirth\packages\!PACKAGE_ID!.!PACKAGE_VERSION!" (
        echo [NewZoneEngineBuild] Missing: AORebirth\packages\!PACKAGE_ID!.!PACKAGE_VERSION!
        set MISSING_PACKAGES=1
    )
)
exit /b 0
