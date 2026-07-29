@echo off
echo === https probe detail ===
curl.exe -k -s -D - -o NUL -H "Host: aomarket.funcom.com" https://127.0.0.1/
echo.
curl.exe -k -s -D - -o NUL -H "Host: uwg.trade.omni-rk" https://127.0.0.1/
echo.
echo === http content sniff ===
curl.exe -s -H "Host: aomarket.funcom.com" http://127.0.0.1/ | findstr /i /c:"Market Account" /c:"title"
echo === ssl vhost ===
findstr /i /c:"443" /c:"SSL" /c:"aomarket" /c:"VirtualHost" C:\xampp\apache\conf\extra\httpd-ssl.conf
echo === recent apache access ===
powershell -NoProfile -Command "Get-Content 'C:\xampp\apache\logs\access.log' -Tail 30"
echo === recent apache error ===
powershell -NoProfile -Command "Get-Content 'C:\xampp\apache\logs\error.log' -Tail 20"
