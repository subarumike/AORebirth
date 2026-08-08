@echo off
setlocal
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
  "%%i" "%~dp0_probe_miy2\ProbeNano.csproj" /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
  if errorlevel 1 exit /b 1
  pushd "%~dp0..\AORebirth\Built\Debug"
  "%~dp0_probe_miy2\bin\Debug\ProbeNano154914.exe"
  popd
  exit /b %ERRORLEVEL%
)
echo MSBuild not found
exit /b 1
