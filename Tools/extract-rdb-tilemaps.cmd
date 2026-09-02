@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

dotnet build Tools\RDBDataExtractor\RDBDataExtractor.csproj -c Debug --nologo -v:q
if errorlevel 1 (
  popd
  exit /b 1
)

Tools\RDBDataExtractor\bin\Debug\net10.0\RDBDataExtractor.exe %*
set "result=%ERRORLEVEL%"
popd
exit /b %result%
