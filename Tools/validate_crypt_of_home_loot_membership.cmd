@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

MSBuild.exe Tools\CryptOfHomeLootMembershipValidator\CryptOfHomeLootMembershipValidator.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 (
  popd
  exit /b 1
)

Tools\CryptOfHomeLootMembershipValidator\bin\Debug\CryptOfHomeLootMembershipValidator.exe %*
set "result=%ERRORLEVEL%"
popd
exit /b %result%
