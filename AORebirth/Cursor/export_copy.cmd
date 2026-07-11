@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "SRC=c:\Users\nermi\source\repos\AORebirth\AORebirth\"
set "DST=c:\Users\nermi\source\repos\AORebirth\AORebirth\Cursor\"
set "LIST=%~dp0MANIFEST.txt"
set /a OK=0
set /a FAIL=0
set /a SKIP=0

for /f "usebackq tokens=* delims=" %%L in ("%LIST%") do (
  set "LINE=%%L"
  if not "!LINE!"=="" if not "!LINE:~0,1!"=="#" (
    if exist "%SRC%!LINE!" (
      for %%D in ("%DST%!LINE!") do mkdir "%%~dpD" 2>nul
      copy /Y "%SRC%!LINE!" "%DST%!LINE!" >nul
      if errorlevel 1 (
        echo FAILED: !LINE!
        set /a FAIL+=1
      ) else (
        echo OK: !LINE!
        set /a OK+=1
      )
    ) else (
      echo MISSING source: !LINE!
      set /a FAIL+=1
    )
  ) else (
    set /a SKIP+=1
  )
)

echo.
echo Export done: !OK! copied, !FAIL! failed.
endlocal
