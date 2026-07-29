@echo off
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
  echo Using MSBuild: %%i
  "%%i" "%USERPROFILE%\source\repos\AOSharp\AOSharp.sln" /t:AOSharp_Common;AOSharp_Bootstrap /p:Configuration=Debug /v:m
  exit /b %ERRORLEVEL%
)
echo MSBuild not found
exit /b 1
