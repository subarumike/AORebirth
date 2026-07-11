@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "SRC=%~dp0"
set "DST=%~dp0..\"
set "LIST=%~dp0MANIFEST.txt"
set /a OK=0
set /a FAIL=0

if not exist "%DST%Server\ZoneEngine\ZoneEngine.csproj" (
  echo ERROR: Project root not found at %DST%
  exit /b 1
)

echo Applying snapshot from %SRC% to %DST%
echo Back up your local files first.
pause

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
      echo MISSING in Cursor: !LINE!
      set /a FAIL+=1
    )
  )
)

echo.
echo Apply done: !OK! copied, !FAIL! failed. Rebuild ZoneEngine + AORebirth.Database + AORebirth.Stats.
endlocal
