@echo off
echo === httpd ===
tasklist /FI "IMAGENAME eq httpd.exe"
echo === hosts ===
findstr /i "aomarket omni-rk" C:\Windows\System32\drivers\etc\hosts
echo === vhosts ===
findstr /i "aomarket DocumentRoot ServerName ServerAlias" C:\xampp\apache\conf\extra\httpd-vhosts.conf
echo === probe ===
curl.exe -s -o NUL -w "aomarket=%%{http_code} " -H "Host: aomarket.funcom.com" http://127.0.0.1/
curl.exe -s -o NUL -w "vault=%%{http_code} " -H "Host: aomarket.funcom.com" http://127.0.0.1/vault.php
curl.exe -s -o NUL -w "https-try=%%{http_code}\n" -k -H "Host: aomarket.funcom.com" https://127.0.0.1/ 2>nul
echo === ping hosts ===
ping -n 1 aomarket.funcom.com
ping -n 1 uwg.trade.omni-rk
