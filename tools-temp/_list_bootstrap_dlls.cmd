@echo off
setlocal
set "OUT=%USERPROFILE%\source\repos\AORebirth\tools-temp\_bootstrap_dlls.txt"
echo. > "%OUT%"
echo === all AOSharp.Bootstrap.dll ===>>"%OUT%"
dir /s /b C:\Users\nermi\source\repos\AOSharp\*AOSharp.Bootstrap.dll >>"%OUT%" 2>nul
dir /s /b C:\Users\nermi\source\repos\aosharp\*AOSharp.Bootstrap.dll >>"%OUT%" 2>nul
echo.>>"%OUT%"
echo === hashes ===>>"%OUT%"
for /f "delims=" %%f in ('dir /s /b C:\Users\nermi\source\repos\AOSharp\*AOSharp.Bootstrap.dll 2^>nul') do (
  echo %%f>>"%OUT%"
  certutil -hashfile "%%f" MD5 | findstr /v "CertUtil hashfile" >>"%OUT%"
  echo.>>"%OUT%"
)
type "%OUT%"
