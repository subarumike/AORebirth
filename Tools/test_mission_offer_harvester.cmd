@echo off
setlocal
cd /d "%~dp0.."
cmd /d /c MSBuild.exe Tools\AOSharpMissionOfferHarvester.OfflineTests\AOSharpMissionOfferHarvester.OfflineTests.csproj /t:Build /p:Configuration=Release /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b 1
Tools\AOSharpMissionOfferHarvester.OfflineTests\bin\Release\AOSharpMissionOfferHarvester.OfflineTests.exe
if errorlevel 1 exit /b 1
exit /b 0
