@echo off
setlocal
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
  "%%i" "%~dp0..\AORebirth\Server\ZoneEngine\ZoneEngine.csproj" /p:Configuration=Debug /m:1 /nr:false /v:minimal
  exit /b %ERRORLEVEL%
)
echo MSBuild not found
exit /b 1
