@echo off
set MARKET=C:\xampp\htdocs\market
set ROOTAPP=C:\xampp\htdocs\index.app
set SRC=C:\Users\nermi\source\repos\AORebirth\tools-temp\gmi-local-web
set HTTPD=C:\xampp\apache\bin\httpd.exe

echo Sync GMI assets into htdocs\market ...
xcopy /E /Y /I /Q "%SRC%\*" "%MARKET%\"
copy /Y "%MARKET%\index.html" "%MARKET%\index.app" >nul

echo Build C:\xampp\htdocs\index.app from market HTML with base=/market/ ...
powershell -NoProfile -File "%~dp0_tmp_fix_index_app_base.ps1"

echo Write htdocs .htaccess DirectoryIndex ...
(
echo DirectoryIndex index.app index.php index.html
echo AddType text/html .app
) > C:\xampp\htdocs\.htaccess

"%HTTPD%" -t
if errorlevel 1 exit /b 1

taskkill /F /IM httpd.exe >nul 2>&1
ping -n 3 127.0.0.1 >nul
start "xampp-apache" /D C:\xampp "%HTTPD%"
ping -n 4 127.0.0.1 >nul

echo.
echo === verify which file is served ===
curl.exe -s -o NUL -w "uwg /index.app=%%{http_code}\n" -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.app
curl.exe -s -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.app | findstr /i /c:"base href" /c:"Market Account" /c:"Not Found" /c:"GMI stub"
curl.exe -s -o NUL -w "uwg /market/vault.php=%%{http_code}\n" -H "Host: uwg.trade.omni-rk" "http://127.0.0.1/market/vault.php?id=12"
echo Root index.app size:
dir "%ROOTAPP%"
echo Done. Entry=C:\xampp\htdocs\index.app  Assets=C:\xampp\htdocs\market\
