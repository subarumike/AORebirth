@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

set "catalogDotnet=dotnet"
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "catalogDotnet=%ProgramFiles%\dotnet\dotnet.exe"
"%catalogDotnet%" build Tools\ItemExpansionCatalog\ItemExpansionCatalog.csproj -c Debug --nologo -v:q
if errorlevel 1 (
  popd
  exit /b 1
)

Tools\ItemExpansionCatalog\bin\Debug\net10.0\ItemExpansionCatalog.exe %*
set "result=%ERRORLEVEL%"
popd
exit /b %result%
