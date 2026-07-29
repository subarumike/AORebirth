@echo off
C:\xampp\apache\bin\httpd.exe -t
if errorlevel 1 exit /b 1
taskkill /F /IM httpd.exe
ping -n 3 127.0.0.1 >nul
start "xampp-apache" /D C:\xampp C:\xampp\apache\bin\httpd.exe
ping -n 4 127.0.0.1 >nul
tasklist /FI "IMAGENAME eq httpd.exe"
curl.exe -s -o NUL -w "aomarket=%%{http_code} " -H "Host: aomarket.funcom.com" http://127.0.0.1/
curl.exe -s -o NUL -w "vault=%%{http_code} " -H "Host: aomarket.funcom.com" http://127.0.0.1/vault.php
curl.exe -s -o NUL -w "uwg=%%{http_code} " -H "Host: uwg.trade.omni-rk" http://127.0.0.1/
curl.exe -s -o NUL -w "local=%%{http_code}\n" -H "Host: localhost" http://127.0.0.1/
curl.exe -s -H "Host: aomarket.funcom.com" http://127.0.0.1/vault.php?id=12 > C:\xampp\htdocs\market\data\_agent_vault_probe.json
type C:\xampp\htdocs\market\data\_agent_vault_probe.json
