@echo off
setlocal EnableExtensions DisableDelayedExpansion
pushd "%~dp0.." >nul
if errorlevel 1 exit /b 1
dotnet run --project Tools\NavMeshGenerator\NavMeshGenerator.csproj --configuration Release -- %*
set "RESULT=%ERRORLEVEL%"
popd >nul
exit /b %RESULT%
