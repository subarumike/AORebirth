@echo off
setlocal
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSB=%%i"
if not defined MSB (
  echo MSBuild not found
  exit /b 1
)
"%MSB%" "%~dp0..\AORebirth\Server\ChatEngine\ChatEngine.csproj" /p:Configuration=Debug /v:m /nologo /m:1 /p:BuildInParallel=false
exit /b %ERRORLEVEL%
