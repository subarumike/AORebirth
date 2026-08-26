@echo off
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
if not "%AO_REBIRTH_GENERATED_COMBAT_LEASE_DELEGATION%"=="" (
    %AO_REBIRTH_PYTHON% "%~dp0generated_combat_pipeline.py" --_validate-read-delegation
    if errorlevel 1 exit /b 1
    goto :generated_combat_read_lease_acquired
)
%AO_REBIRTH_PYTHON% "%~dp0generated_combat_pipeline.py" --run-read-lease -- "%ComSpec%" /d /c "%~f0" %*
exit /b %errorlevel%

:generated_combat_read_lease_acquired
setlocal EnableExtensions EnableDelayedExpansion

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "MSBUILD="
set RESTORE_LOG=build_package_restore.log
set RESTORE_CMD=%TEMP%\aorebirth_package_restore_%RANDOM%.cmd
set RESTORE_DONE=%TEMP%\aorebirth_package_restore_done_%RANDOM%.tmp
set RESTORE_STATUS=%TEMP%\aorebirth_package_restore_status_%RANDOM%.tmp
set RESTORE_TIMEOUT_SECONDS=120
set RESTORE_POLL_SECONDS=5
set /A RESTORE_PING_COUNT=%RESTORE_POLL_SECONDS%+1

pushd "%~dp0.."
if errorlevel 1 (
    echo [AORebirth Build] Failed to switch to repository root.
    exit /b 1
)

if not exist "%VSWHERE%" (
    echo [AORebirth Build] Visual Studio Installer vswhere.exe was not found.
    popd
    exit /b 1
)

for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -find MSBuild\Current\Bin\MSBuild.exe`) do (
    if not defined MSBUILD set "MSBUILD=%%I"
)

if not defined MSBUILD (
    echo [AORebirth Build] MSBuild.exe was not found in the latest Visual Studio installation.
    popd
    exit /b 1
)

echo [AORebirth Build] Cleaning stale build processes...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1
taskkill /F /T /IM NuGet.exe >nul 2>&1

call :RestoreDependencies
if errorlevel 1 (
    set RESTORE_EXIT=!ERRORLEVEL!
    popd
    exit /b !RESTORE_EXIT!
)

echo [AORebirth Build] Building AORebirth.Core...
"%MSBUILD%" "AORebirth\Libraries\Source\AORebirth.Core\AORebirth.Core.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
set CORE_EXIT=%ERRORLEVEL%
if not "%CORE_EXIT%"=="0" (
    echo [AORebirth Build] AORebirth.Core failed with exit code %CORE_EXIT%.
    popd
    exit /b %CORE_EXIT%
)

echo [AORebirth Build] Cleaning stale build processes before LoginEngine...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1
taskkill /F /T /IM NuGet.exe >nul 2>&1

echo [AORebirth Build] Building LoginEngine...
"%MSBUILD%" "AORebirth\Server\LoginEngine\LoginEngine.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
set LOGIN_EXIT=%ERRORLEVEL%
if not "%LOGIN_EXIT%"=="0" (
    echo [AORebirth Build] LoginEngine failed with exit code %LOGIN_EXIT%.
    popd
    exit /b %LOGIN_EXIT%
)

echo [AORebirth Build] Cleaning stale build processes before ZoneEngine...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1
taskkill /F /T /IM NuGet.exe >nul 2>&1

echo [AORebirth Build] Building ZoneEngine...
"%MSBUILD%" "AORebirth\Server\ZoneEngine\ZoneEngine.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
set ZONE_EXIT=%ERRORLEVEL%
if not "%ZONE_EXIT%"=="0" (
    echo [AORebirth Build] ZoneEngine failed with exit code %ZONE_EXIT%.
    popd
    exit /b %ZONE_EXIT%
)

set "ZONE_OUTPUT=%CD%\AORebirth\Built\Debug"
set "PLACEMENT_OUTPUT=%ZONE_OUTPUT%\Content\Official\PlayfieldPlacements"
set "PLACEMENT_MANIFEST=%PLACEMENT_OUTPUT%\official-placement-build-manifest.json"
set "PLACEMENT_PROVENANCE=%PLACEMENT_OUTPUT%\PLACEMENT_PROVENANCE.env"
set "SOURCE_SHA="
for /f "usebackq delims=" %%I in (`git rev-parse HEAD`) do set "SOURCE_SHA=%%I"
if not defined SOURCE_SHA (
    echo [AORebirth Build] Official placement validation could not resolve the source SHA.
    popd
    exit /b 1
)
if not exist "%ZONE_OUTPUT%\ZoneEngine.exe" (
    echo [AORebirth Build] Official placement validation could not find the built ZoneEngine.exe.
    popd
    exit /b 1
)

echo [AORebirth Build] Validating packaged official playfield placements...
"%ZONE_OUTPUT%\ZoneEngine.exe" --validate-official-placements --source-sha "%SOURCE_SHA%" --placement-manifest-output "%PLACEMENT_MANIFEST%" --placement-provenance-output "%PLACEMENT_PROVENANCE%" --build-platform windows
set PLACEMENT_EXIT=%ERRORLEVEL%
if not "%PLACEMENT_EXIT%"=="0" (
    echo [AORebirth Build] Official placement validation failed with exit code %PLACEMENT_EXIT%.
    popd
    exit /b %PLACEMENT_EXIT%
)

echo [AORebirth Build] Building DatabasePreflight...
"%MSBUILD%" "Tools\DatabasePreflight\DatabasePreflight.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
set PREFLIGHT_EXIT=%ERRORLEVEL%
if not "%PREFLIGHT_EXIT%"=="0" (
    echo [AORebirth Build] DatabasePreflight failed with exit code %PREFLIGHT_EXIT%.
    popd
    exit /b %PREFLIGHT_EXIT%
)

echo [AORebirth Build] Building WebEngine...
"%MSBUILD%" "AORebirth\Server\WebEngine\WebEngine.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
set WEB_EXIT=%ERRORLEVEL%
if not "%WEB_EXIT%"=="0" (
    echo [AORebirth Build] WebEngine failed with exit code %WEB_EXIT%.
    popd
    exit /b %WEB_EXIT%
)

echo [AORebirth Build] Cleaning stale build processes after successful build...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1
taskkill /F /T /IM NuGet.exe >nul 2>&1

echo [AORebirth Build] Build succeeded.
popd
exit /b 0

:RestoreDependencies
call :VerifyPackagesRestored
if not errorlevel 1 exit /b 0

if exist "%RESTORE_DONE%" del /q "%RESTORE_DONE%" >nul 2>&1
if exist "%RESTORE_STATUS%" del /q "%RESTORE_STATUS%" >nul 2>&1
if exist "%RESTORE_CMD%" del /q "%RESTORE_CMD%" >nul 2>&1
if exist "%RESTORE_LOG%" del /q "%RESTORE_LOG%" >nul 2>&1

echo [AORebirth Build] Restoring packages explicitly with MSBuild Restore...
echo [AORebirth Build] Restore log: %CD%\%RESTORE_LOG%
(
    echo @echo off
    echo "%MSBUILD%" "%CD%\AORebirth\AORebirth.sln" /t:Restore /p:RestorePackagesConfig=true /m:1 /nr:false /v:minimal ^> "%CD%\%RESTORE_LOG%" 2^>^&1
    echo echo %%ERRORLEVEL%% ^> "%RESTORE_STATUS%"
    echo type nul ^> "%RESTORE_DONE%"
) > "%RESTORE_CMD%"
start "" /B cmd /d /c call "%RESTORE_CMD%"

set /A RESTORE_ELAPSED=0
:RestoreWait
if exist "%RESTORE_DONE%" goto RestoreFinished
if %RESTORE_ELAPSED% GEQ %RESTORE_TIMEOUT_SECONDS% goto RestoreTimedOut
echo [AORebirth Build] MSBuild restore still running; elapsed %RESTORE_ELAPSED%s.
ping -n %RESTORE_PING_COUNT% 127.0.0.1 >nul
set /A RESTORE_ELAPSED+=%RESTORE_POLL_SECONDS%
goto RestoreWait

:RestoreTimedOut
echo [AORebirth Build] MSBuild restore timed out after %RESTORE_TIMEOUT_SECONDS%s.
echo [AORebirth Build] Killing build processes and failing build validation.
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1
taskkill /F /T /IM NuGet.exe >nul 2>&1
exit /b 1

:RestoreFinished
set RESTORE_EXIT=1
if exist "%RESTORE_STATUS%" set /P RESTORE_EXIT=<"%RESTORE_STATUS%"
if exist "%RESTORE_CMD%" del /q "%RESTORE_CMD%" >nul 2>&1
if exist "%RESTORE_DONE%" del /q "%RESTORE_DONE%" >nul 2>&1
if exist "%RESTORE_STATUS%" del /q "%RESTORE_STATUS%" >nul 2>&1
if not "%RESTORE_EXIT%"=="0" (
    echo [AORebirth Build] MSBuild restore failed with exit code %RESTORE_EXIT%.
    if exist "%RESTORE_LOG%" type "%RESTORE_LOG%"
    exit /b %RESTORE_EXIT%
)

echo [AORebirth Build] MSBuild restore completed.
if exist "%RESTORE_LOG%" type "%RESTORE_LOG%"
exit /b 0

:VerifyPackagesRestored
set MISSING_PACKAGES=0
echo [AORebirth Build] Checking package folders for AORebirth.Core and ZoneEngine dependencies...
call :CheckPackageConfig "AORebirth\Libraries\Source\AORebirth.Core\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\AORebirth.Database\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\AORebirth.Interfaces\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\AORebirth.Communication\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\Cell.Core\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\Exceptions\packages.config"
call :CheckPackageConfig "AORebirth\Libraries\Source\Utility\packages.config"
call :CheckPackageConfig "AORebirth\Server\ZoneEngine\packages.config"
call :CheckPackageConfig "AORebirth\Server\WebEngine\packages.config"

if "%MISSING_PACKAGES%"=="0" (
    echo [AORebirth Build] All required package folders already exist in AORebirth\packages.
    echo [AORebirth Build] Skipping explicit restore; required packages are already present.
    exit /b 0
)

echo [AORebirth Build] Missing package folders detected; running explicit MSBuild restore.
exit /b 1

:CheckPackageConfig
set PACKAGE_CONFIG=%~1
if not exist "%PACKAGE_CONFIG%" (
    echo [AORebirth Build] Missing package config: %PACKAGE_CONFIG%
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
        echo [AORebirth Build] Missing package folder: AORebirth\packages\!PACKAGE_ID!.!PACKAGE_VERSION!
        set MISSING_PACKAGES=1
    )
)

exit /b 0
