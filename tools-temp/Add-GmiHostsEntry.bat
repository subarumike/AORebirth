@echo off
:: Right-click this file -> Run as administrator
echo Before:
findstr /i "uwg.trade.omni-rk" "%SystemRoot%\System32\drivers\etc\hosts"
findstr /i "uwg.trade.omni-rk" "%SystemRoot%\System32\drivers\etc\hosts" >nul
if %ERRORLEVEL%==0 (
  echo Entry already present.
) else (
  echo.>>"%SystemRoot%\System32\drivers\etc\hosts"
  echo 127.0.0.1    uwg.trade.omni-rk>>"%SystemRoot%\System32\drivers\etc\hosts"
  echo Added hosts line.
)
echo.
echo After:
findstr /i "uwg.trade.omni-rk" "%SystemRoot%\System32\drivers\etc\hosts"
echo.
ipconfig /flushdns
echo.
ping -n 1 uwg.trade.omni-rk
echo.
pause
