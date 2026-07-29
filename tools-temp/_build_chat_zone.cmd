@echo off
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
"%MSBUILD%" "%USERPROFILE%\source\repos\AORebirth\AORebirth\Server\ChatEngine\ChatEngine.csproj" /p:Configuration=Debug /v:m
if errorlevel 1 exit /b 1
"%MSBUILD%" "%USERPROFILE%\source\repos\AORebirth\AORebirth\Server\ZoneEngine\ZoneEngine.csproj" /p:Configuration=Debug /v:m
exit /b %ERRORLEVEL%
