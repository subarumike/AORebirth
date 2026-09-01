@echo off
setlocal
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1

cd /d "%~dp0.."
if "%MIKE_AOSHARP_RUNTIME%"=="" set "MIKE_AOSHARP_RUNTIME=D:\AOTools\ReadyToUse"
set "MALIS_WORK_ROOT=%CD%\tools-temp\MalisLiveBuild"
set "MALIS_PACKAGE_ROOT=%CD%\build-verify\MalisMissionLive"
set "MALIS_SOURCE_ROOT=%MALIS_WORK_ROOT%\source\malis-3ac9943a"
set "MALIS_METADATA=%MALIS_WORK_ROOT%\compatibility-metadata.json"

%AO_REBIRTH_PYTHON% Tools\malis_live_build.py --prepare --runtime "%MIKE_AOSHARP_RUNTIME%" --work-root "%MALIS_WORK_ROOT%" --package-root "%MALIS_PACKAGE_ROOT%" --metadata "%MALIS_METADATA%"
if errorlevel 1 exit /b 1

cmd /d /c MSBuild.exe "%MALIS_SOURCE_ROOT%\Malis Mission Roller 2.csproj" /t:Rebuild /p:Configuration=Release /p:PlatformTarget=x86 /p:ReferencePath="%MIKE_AOSHARP_RUNTIME%" /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b 1

cmd /d /c MSBuild.exe Tools\AOSharpMissionOfferHarvester\AOSharpMissionOfferHarvester.csproj /t:Rebuild /p:Configuration=Release /p:PlatformTarget=x86 /p:AOSharpSdkDir="%MIKE_AOSHARP_RUNTIME%" /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b 1

cmd /d /c MSBuild.exe Tools\MalisLiveCompatibilityCheck\MalisLiveCompatibilityCheck.csproj /t:Rebuild /p:Configuration=Release /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b 1

cmd /d /c Tools\MalisLiveCompatibilityCheck\bin\Release\MalisLiveCompatibilityCheck.exe "%MIKE_AOSHARP_RUNTIME%" "%MALIS_SOURCE_ROOT%\bin\Release\Malis Mission Roller 2.dll" "Tools\AOSharpMissionOfferHarvester\bin\Release\AOSharpMissionOfferHarvester.dll" "%MALIS_METADATA%"
if errorlevel 1 exit /b 1

%AO_REBIRTH_PYTHON% Tools\malis_live_build.py --package --runtime "%MIKE_AOSHARP_RUNTIME%" --work-root "%MALIS_WORK_ROOT%" --package-root "%MALIS_PACKAGE_ROOT%" --metadata "%MALIS_METADATA%"
if errorlevel 1 exit /b 1

cmd /d /c Tools\MalisLiveCompatibilityCheck\bin\Release\MalisLiveCompatibilityCheck.exe "%MIKE_AOSHARP_RUNTIME%" "%MALIS_PACKAGE_ROOT%\Malis Mission Roller 2\Malis Mission Roller 2.dll" "%MALIS_PACKAGE_ROOT%\MissionOfferHarvester\AOSharpMissionOfferHarvester.dll" "%MALIS_METADATA%"
if errorlevel 1 exit /b 1

%AO_REBIRTH_PYTHON% Tools\malis_live_build.py --check --runtime "%MIKE_AOSHARP_RUNTIME%" --work-root "%MALIS_WORK_ROOT%" --package-root "%MALIS_PACKAGE_ROOT%" --metadata "%MALIS_METADATA%"
if errorlevel 1 exit /b 1

echo MALIS_LIVE_BUILD_PACKAGE=PASS
exit /b 0
