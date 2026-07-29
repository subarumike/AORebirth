@echo off
echo === htdocs root market files ===
if exist C:\xampp\htdocs\index.php (echo htdocs\index.php EXISTS) else (echo htdocs\index.php MISSING)
if exist C:\xampp\htdocs\vault.php (echo htdocs\vault.php EXISTS) else (echo htdocs\vault.php MISSING)
if exist C:\xampp\htdocs\market\index.php (echo market\index.php EXISTS) else (echo market\index.php MISSING)
echo === curl root vs market ===
curl.exe -s -o NUL -w "root %%{http_code}\n" http://127.0.0.1/
curl.exe -s -o NUL -w "market %%{http_code}\n" http://127.0.0.1/market/
curl.exe -s -o NUL -w "aomarket-root %%{http_code}\n" -H "Host: aomarket.funcom.com" http://127.0.0.1/
curl.exe -s -o NUL -w "uwg-root %%{http_code}\n" -H "Host: uwg.trade.omni-rk" http://127.0.0.1/
curl.exe -s -o NUL -w "aomarket-market %%{http_code}\n" -H "Host: aomarket.funcom.com" http://127.0.0.1/market/
echo === last MarketSend ===
powershell -NoProfile -Command "Select-String -Path 'AORebirth\Built\Debug\ZoneEngineLog.txt' -Pattern '-> MarketSend' | Select-Object -Last 8 | ForEach-Object { $_.Line }"
echo === apache vhosts snippet ===
findstr /i /c:"aomarket" /c:"omni-rk" /c:"market" C:\xampp\apache\conf\extra\httpd-vhosts.conf
findstr /i /c:"aomarket" /c:"omni-rk" /c:"DocumentRoot" C:\xampp\apache\conf\httpd.conf
