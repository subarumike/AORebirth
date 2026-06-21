@echo off
setlocal EnableExtensions EnableDelayedExpansion

set MSBUILD=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe
set NUGET=AORebirth\.nuget\NuGet.exe
set RESTORE_LOG=build_nuget_restore.log
set RESTORE_CMD=%TEMP%\aorebirth_nuget_restore_%RANDOM%.cmd
set RESTORE_DONE=%TEMP%\aorebirth_nuget_restore_done_%RANDOM%.tmp
set RESTORE_STATUS=%TEMP%\aorebirth_nuget_restore_status_%RANDOM%.tmp
set RESTORE_TIMEOUT_SECONDS=120
set RESTORE_POLL_SECONDS=5
set /A RESTORE_PING_COUNT=%RESTORE_POLL_SECONDS%+1

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

if not exist "%NUGET%" (
    echo [AORebirth Build] NuGet.exe not found: %NUGET%
    popd
    exit /b 1
)

echo [AORebirth Build] Cleaning stale build processes...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1
taskkill /F /T /IM NuGet.exe >nul 2>&1

call :RestorePackages
if errorlevel 1 (
    set RESTORE_EXIT=!ERRORLEVEL!
    popd
    exit /b !RESTORE_EXIT!
)

echo [AORebirth Build] Building AORebirth.Core...
"%MSBUILD%" "AORebirth\Libraries\Source\AORebirth.Core\AORebirth.Core.csproj" /t:Build /p:Configuration=Debug /p:RestorePackages=false /m:1 /nr:false /v:minimal
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
taskkill /F /T /IM NuGet.exe >nul 2>&1

echo [AORebirth Build] Building ZoneEngine...
"%MSBUILD%" "AORebirth\Server\ZoneEngine\ZoneEngine.csproj" /t:Build /p:Configuration=Debug /p:RestorePackages=false /m:1 /nr:false /v:minimal
set ZONE_EXIT=%ERRORLEVEL%
if not "%ZONE_EXIT%"=="0" (
    echo [AORebirth Build] ZoneEngine failed with exit code %ZONE_EXIT%.
    popd
    exit /b %ZONE_EXIT%
)

echo [AORebirth Build] Cleaning stale build processes after successful build...
taskkill /F /T /IM MSBuild.exe >nul 2>&1
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM VBCSCompiler.exe >nul 2>&1
taskkill /F /T /IM NuGet.exe >nul 2>&1

echo [AORebirth Build] Build succeeded.
popd
exit /b 0

:RestorePackages
call :VerifyPackagesRestored
if not errorlevel 1 exit /b 0

if exist "%RESTORE_DONE%" del /q "%RESTORE_DONE%" >nul 2>&1
if exist "%RESTORE_STATUS%" del /q "%RESTORE_STATUS%" >nul 2>&1
if exist "%RESTORE_CMD%" del /q "%RESTORE_CMD%" >nul 2>&1
if exist "%RESTORE_LOG%" del /q "%RESTORE_LOG%" >nul 2>&1

echo [AORebirth Build] Restoring NuGet packages explicitly...
echo [AORebirth Build] Restore log: %CD%\%RESTORE_LOG%
(
    echo @echo off
    echo "%CD%\%NUGET%" restore "%CD%\AORebirth\AORebirth.sln" -NonInteractive -Verbosity normal -ConfigFile "%CD%\AORebirth\.nuget\NuGet.Config" -DisableParallelProcessing ^> "%CD%\%RESTORE_LOG%" 2^>^&1
    echo echo %%ERRORLEVEL%% ^> "%RESTORE_STATUS%"
    echo type nul ^> "%RESTORE_DONE%"
) > "%RESTORE_CMD%"
start "" /B cmd /d /c call "%RESTORE_CMD%"

set /A RESTORE_ELAPSED=0
:RestoreWait
if exist "%RESTORE_DONE%" goto RestoreFinished
if %RESTORE_ELAPSED% GEQ %RESTORE_TIMEOUT_SECONDS% goto RestoreTimedOut
echo [AORebirth Build] NuGet restore still running; elapsed %RESTORE_ELAPSED%s.
ping -n %RESTORE_PING_COUNT% 127.0.0.1 >nul
set /A RESTORE_ELAPSED+=%RESTORE_POLL_SECONDS%
goto RestoreWait

:RestoreTimedOut
echo [AORebirth Build] NuGet restore timed out after %RESTORE_TIMEOUT_SECONDS%s.
echo [AORebirth Build] Killing NuGet.exe and failing build validation.
taskkill /F /T /IM NuGet.exe >nul 2>&1
exit /b 1

:RestoreFinished
set RESTORE_EXIT=1
if exist "%RESTORE_STATUS%" set /P RESTORE_EXIT=<"%RESTORE_STATUS%"
if exist "%RESTORE_CMD%" del /q "%RESTORE_CMD%" >nul 2>&1
if exist "%RESTORE_DONE%" del /q "%RESTORE_DONE%" >nul 2>&1
if exist "%RESTORE_STATUS%" del /q "%RESTORE_STATUS%" >nul 2>&1
if not "%RESTORE_EXIT%"=="0" (
    echo [AORebirth Build] NuGet restore failed with exit code %RESTORE_EXIT%.
    if exist "%RESTORE_LOG%" type "%RESTORE_LOG%"
    exit /b %RESTORE_EXIT%
)

echo [AORebirth Build] NuGet restore completed.
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

if "%MISSING_PACKAGES%"=="0" (
    echo [AORebirth Build] All required package folders already exist in AORebirth\packages.
    echo [AORebirth Build] Skipping NuGet.exe restore; build-time RestorePackages remains disabled.
    exit /b 0
)

echo [AORebirth Build] Missing package folders detected; running NuGet.exe restore.
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
