@echo off
setlocal
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSB=%%i"
if not defined MSB (
  echo MSBuild not found
  exit /b 1
)
"%MSB%" "%~dp0PerkActionExtract\PerkActionExtract.csproj" /p:Configuration=Debug /v:m /nologo
if errorlevel 1 exit /b 1
"%~dp0PerkActionExtract\bin\Debug\PerkActionExtract.exe" scan-addaction
exit /b %ERRORLEVEL%
