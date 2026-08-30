@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

MSBuild.exe Tools\SubwayLootMembershipValidator\SubwayLootMembershipValidator.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 (
  popd
  exit /b 1
)

Tools\SubwayLootMembershipValidator\bin\Debug\SubwayLootMembershipValidator.exe "%CD%"
set "result=%ERRORLEVEL%"
popd
exit /b %result%
