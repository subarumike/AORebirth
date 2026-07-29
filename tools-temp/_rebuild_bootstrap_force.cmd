@echo off
setlocal
call "%~dp0_list_bootstrap_dlls.cmd"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"
"%MSBUILD%" "%USERPROFILE%\source\repos\AOSharp\AOSharp.Bootstrap\AOSharp.Bootstrap.csproj" /t:Rebuild /p:Configuration=Debug /v:m
if errorlevel 1 exit /b 1
copy /Y "%USERPROFILE%\source\repos\AOSharp\AOSharp.Bootstrap\bin\Debug\AOSharp.Bootstrap.dll" "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharp.Bootstrap.dll"
dir "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharp.Bootstrap.dll"
dir "%USERPROFILE%\source\repos\AOSharp\AOSharp\bin\Debug\AOSharp.exe"
exit /b 0
