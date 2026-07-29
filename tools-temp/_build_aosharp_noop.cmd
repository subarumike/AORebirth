@echo off
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
if not defined MSBUILD (
  echo MSBuild not found
  exit /b 1
)
if not exist "%USERPROFILE%\source\repos\AOSharp\AOSharp.Core\bin\Debug\AOSharp.Core.dll" (
  "%MSBUILD%" "%USERPROFILE%\source\repos\AOSharp\AOSharp.sln" /t:AOSharp_Core /p:Configuration=Debug /v:m
  if errorlevel 1 exit /b 1
)
"%MSBUILD%" "%USERPROFILE%\source\repos\AOSharp\AOSharpNoOp\AOSharpNoOp.csproj" /p:Configuration=Debug /v:m
if errorlevel 1 exit /b 1
copy /Y "%USERPROFILE%\source\repos\AOSharp\AOSharpNoOp\bin\Debug\AOSharpNoOp.dll" "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharpNoOp.dll"
certutil -hashfile "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharpNoOp.dll" MD5
exit /b 0
