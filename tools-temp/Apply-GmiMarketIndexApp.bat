@echo off
set SRC=C:\Users\nermi\source\repos\AORebirth\tools-temp\gmi-local-web
set DST=C:\xampp\htdocs\market
set HTTPD=C:\xampp\apache\bin\httpd.exe

echo Syncing GMI into market...
xcopy /E /Y /I /Q "%SRC%\*" "%DST%\"
copy /Y "%DST%\index.html" "%DST%\index.app" >nul

echo Writing market .htaccess...
(
echo DirectoryIndex index.app index.php index.html
echo AddType text/html .app
echo.
echo ^<IfModule mod_headers.c^>
echo   Header set Cache-Control "no-store, no-cache, must-revalidate, max-age=0"
echo   Header set Pragma "no-cache"
echo   Header set Expires "0"
echo ^</IfModule^>
) > "%DST%\.htaccess"

echo Writing localhost /index.app redirect into /market/...
(
echo ^<!DOCTYPE html^>
echo ^<html^>^<head^>
echo ^<meta http-equiv="refresh" content="0;url=/market/index.app"^>
echo ^<title^>GMI^</title^>
echo ^</head^>^<body^>
echo ^<p^>^<a href="/market/index.app"^>Open GMI Market^</a^>^</p^>
echo ^</body^>^</html^>
) > C:\xampp\htdocs\index.app

"%HTTPD%" -t
if errorlevel 1 exit /b 1

taskkill /F /IM httpd.exe >nul 2>&1
ping -n 3 127.0.0.1 >nul
start "xampp-apache" /D C:\xampp "%HTTPD%"
ping -n 4 127.0.0.1 >nul

echo.
curl.exe -s -o NUL -w "uwg /index.app=%%{http_code} " -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.app
curl.exe -s -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.app | findstr /i /c:"Market Account" /c:"Not Found" /c:"GMI stub" /c:"title"
curl.exe -s -o NUL -w "aomarket /index.app=%%{http_code} " -H "Host: aomarket.funcom.com" http://127.0.0.1/index.app
curl.exe -s -o NUL -w "local /market/index.app=%%{http_code}\n" -H "Host: localhost" http://127.0.0.1/market/index.app
