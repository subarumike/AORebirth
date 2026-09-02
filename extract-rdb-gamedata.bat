@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
if "%~1"=="" (
  echo Usage: extract-rdb-gamedata.bat ^<AO path^> [RDBDataExtractor options]
  echo Example: extract-rdb-gamedata.bat "C:\Funcom\Anarchy Online" --tilemap-id 1930
  exit /b 2
)

set "AO_PATH=%~1"
shift

pushd "%ROOT%"
if errorlevel 1 exit /b 1

dotnet build "%ROOT%Tools\RDBDataExtractor\RDBDataExtractor.csproj" -c Debug --nologo -v:q
if errorlevel 1 (
  popd
  exit /b 1
)

"%ROOT%Tools\RDBDataExtractor\bin\Debug\net10.0\RDBDataExtractor.exe" --ao-path "%AO_PATH%" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
