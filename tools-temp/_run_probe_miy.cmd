@echo off
setlocal
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
  "%%i" "%~dp0_probe_miy\ProbeMiy.csproj" /p:Configuration=Debug /v:minimal
  if errorlevel 1 exit /b 1
  "%~dp0_probe_miy\bin\Debug\ProbeMiy.exe"
  exit /b %ERRORLEVEL%
)
echo MSBuild not found
exit /b 1
