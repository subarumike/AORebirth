@echo off
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
if not defined MSBUILD (
  echo MSBuild not found
  exit /b 1
)
"%MSBUILD%" "%USERPROFILE%\source\repos\AOSharp\AOSharp.sln" /t:AOSharp_Common;AOSharp_Bootstrap;AOSharp /p:Configuration=Debug /v:m
if errorlevel 1 exit /b 1
copy /Y "%USERPROFILE%\source\repos\AOSharp\AOSharp.Bootstrap\bin\Debug\AOSharp.Bootstrap.dll" "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharp.Bootstrap.dll"
copy /Y "%USERPROFILE%\source\repos\AOSharp\AOSharp.Common\bin\Debug\AOSharp.Common.dll" "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharp.Common.dll"
echo DONE
dir "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharp.exe"
dir "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharp.Bootstrap.dll"
exit /b 0
