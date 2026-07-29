@echo off
echo === folders under htdocs ===
dir /b /ad C:\xampp\htdocs
echo === index.app locations ===
dir /s /b C:\xampp\htdocs\index.app 2>nul
echo === vhosts ===
type C:\xampp\apache\conf\extra\httpd-vhosts.conf
echo === curl ===
curl.exe -s -D - -o NUL -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.app | findstr /i "HTTP Location Server"
curl.exe -s -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.app | findstr /i /c:"Market Account" /c:"Not Found" /c:"GMI stub" /c:"redirect" /c:"title"
