@echo off
:: Right-click -> Run as administrator
:: Maps ICC-RK VGTP hosts used by Daily Rewards + Item Store to localhost.
setlocal
set HOSTS=%SystemRoot%\System32\drivers\etc\hosts

echo Before:
findstr /i "uwg.daily.icc-rk uwg.store.icc-rk" "%HOSTS%"
echo.

findstr /i /c:"uwg.daily.icc-rk" "%HOSTS%" >nul
if errorlevel 1 (
  echo.>>"%HOSTS%"
  echo 127.0.0.1    uwg.daily.icc-rk>>"%HOSTS%"
  echo Added uwg.daily.icc-rk
) else (
  echo uwg.daily.icc-rk already present.
)

findstr /i /c:"uwg.store.icc-rk" "%HOSTS%" >nul
if errorlevel 1 (
  echo 127.0.0.1    uwg.store.icc-rk>>"%HOSTS%"
  echo Added uwg.store.icc-rk
) else (
  echo uwg.store.icc-rk already present.
)

echo.
echo After:
findstr /i "uwg.daily.icc-rk uwg.store.icc-rk" "%HOSTS%"
echo.
ipconfig /flushdns >nul
ping -n 1 uwg.daily.icc-rk
ping -n 1 uwg.store.icc-rk
echo.
pause
endlocal
