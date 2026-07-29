@echo off
:: Apply / verify GMI Apache vhosts and reload Apache (non-service XAMPP).
:: Safe to re-run. Does not touch Zone engines.
setlocal
set HTTPD=C:\xampp\apache\bin\httpd.exe
set VHOSTS=C:\xampp\apache\conf\extra\httpd-vhosts.conf

if not exist "%HTTPD%" (
  echo ERROR: %HTTPD% not found.
  exit /b 1
)

findstr /i /c:"aomarket.funcom.com" "%VHOSTS%" >nul
if errorlevel 1 (
  echo ERROR: aomarket.funcom.com VirtualHost missing from httpd-vhosts.conf
  exit /b 1
)

echo Testing Apache config...
"%HTTPD%" -t
if errorlevel 1 exit /b 1

echo Reloading Apache (taskkill + start)...
taskkill /F /IM httpd.exe >nul 2>&1
ping -n 3 127.0.0.1 >nul
start "xampp-apache" /D C:\xampp "%HTTPD%"
ping -n 4 127.0.0.1 >nul

echo.
curl.exe -s -o NUL -w "aomarket / = %%{http_code}\n" -H "Host: aomarket.funcom.com" http://127.0.0.1/
curl.exe -s -o NUL -w "aomarket /vault.php = %%{http_code}\n" -H "Host: aomarket.funcom.com" http://127.0.0.1/vault.php
curl.exe -s -o NUL -w "uwg / = %%{http_code}\n" -H "Host: uwg.trade.omni-rk" http://127.0.0.1/
curl.exe -s -o NUL -w "localhost / = %%{http_code} (expect 302 dashboard)\n" -H "Host: localhost" http://127.0.0.1/
echo Done. In-game Market should open GMI at host root now.
endlocal
