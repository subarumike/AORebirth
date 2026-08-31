@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

MSBuild.exe Tools\MercenaryCampLootMembershipValidator\MercenaryCampLootMembershipValidator.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 (
  popd
  exit /b 1
)

Tools\MercenaryCampLootMembershipValidator\bin\Debug\MercenaryCampLootMembershipValidator.exe %*
set "result=%ERRORLEVEL%"
popd
exit /b %result%
