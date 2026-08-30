@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

MSBuild.exe Tools\InnerSanctumLootMembershipValidator\InnerSanctumLootMembershipValidator.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 (
  popd
  exit /b 1
)

Tools\InnerSanctumLootMembershipValidator\bin\Debug\InnerSanctumLootMembershipValidator.exe "%CD%"
set "result=%ERRORLEVEL%"
popd
exit /b %result%
