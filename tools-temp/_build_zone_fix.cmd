@echo off
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
if not defined MSBUILD (
  echo MSBuild not found
  exit /b 1
)
"%MSBUILD%" "%USERPROFILE%\source\repos\AORebirth\AORebirth\Server\ZoneEngine\ZoneEngine.csproj" /p:Configuration=Debug /v:m
exit /b %ERRORLEVEL%
