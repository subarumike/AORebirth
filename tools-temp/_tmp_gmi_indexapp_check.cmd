@echo off
echo === htdocs root ===
dir /b C:\xampp\htdocs\index.app 2>nul
dir /b C:\xampp\htdocs\*.app 2>nul
echo === market ===
dir /b C:\xampp\htdocs\market\index.app 2>nul
dir /b C:\xampp\htdocs\market\*.app 2>nul
dir /b C:\xampp\htdocs\market\index.*
echo === curl index.app ===
curl.exe -s -o NUL -w "uwg /index.app=%%{http_code}\n" -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.app
curl.exe -s -o NUL -w "uwg /=%%{http_code}\n" -H "Host: uwg.trade.omni-rk" http://127.0.0.1/
curl.exe -s -o NUL -w "uwg /index.php=%%{http_code}\n" -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.php
curl.exe -s -o NUL -w "uwg /index.html=%%{http_code}\n" -H "Host: uwg.trade.omni-rk" http://127.0.0.1/index.html
curl.exe -s -o NUL -w "aomarket /index.app=%%{http_code}\n" -H "Host: aomarket.funcom.com" http://127.0.0.1/index.app
echo === vhost snippet ===
findstr /n /i "aomarket omni-rk DocumentRoot ServerName" C:\xampp\apache\conf\extra\httpd-vhosts.conf
