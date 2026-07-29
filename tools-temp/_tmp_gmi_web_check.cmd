@echo off
echo === xampp gmi_db_config ===
type C:\xampp\htdocs\market\gmi_db_config.php
echo.
echo === vault.php via curl identity 12 ===
curl -s "http://127.0.0.1/market/vault.php?id=12&name=Nerko"
echo.
echo === index.php head ===
curl -s -o NUL -w "HTTP %{http_code}\n" "http://127.0.0.1/market/index.php"
curl -s -o NUL -w "HTTP %{http_code} host aomarket\n" -H "Host: aomarket.funcom.com" "http://127.0.0.1/"
curl -s -o NUL -w "HTTP %{http_code} host uwg\n" -H "Host: uwg.trade.omni-rk" "http://127.0.0.1/"
echo === apache running? ===
tasklist /FI "IMAGENAME eq httpd.exe" | findstr /i httpd
tasklist /FI "IMAGENAME eq mysql.exe" | findstr /i mysql
