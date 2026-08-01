@echo off
setlocal EnableExtensions
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "MSBUILD="
for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -find MSBuild\Current\Bin\MSBuild.exe`) do (
  if not defined MSBUILD set "MSBUILD=%%I"
)
if not defined MSBUILD (
  echo MSBuild not found
  exit /b 1
)
"%MSBUILD%" "%~dp0..\AORebirth\Server\ChatEngine\ChatEngine.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
exit /b %ERRORLEVEL%
