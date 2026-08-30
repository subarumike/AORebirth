@echo off
setlocal
cd /d "%~dp0.."

MSBuild.exe Tools\TempleLootMembershipValidator\TempleLootMembershipValidator.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
if errorlevel 1 exit /b 1

Tools\TempleLootMembershipValidator\bin\Debug\TempleLootMembershipValidator.exe "%CD%"
exit /b %errorlevel%
