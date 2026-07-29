@echo off
echo === htdocs top php ===
dir /b C:\xampp\htdocs\*.php
echo === market php ===
dir /b C:\xampp\htdocs\market\*.php
echo === curl vault paths ===
curl.exe -s -o NUL -w " /vault.php %%{http_code}\n" http://127.0.0.1/vault.php
curl.exe -s -o NUL -w " /market/vault.php %%{http_code}\n" http://127.0.0.1/market/vault.php
curl.exe -s -o NUL -w " host aomarket /vault.php %%{http_code}\n" -H "Host: aomarket.funcom.com" http://127.0.0.1/vault.php
curl.exe -s http://127.0.0.1/index.php | findstr /i /c:"GMI" /c:"Market" /c:"Omni" /c:"xampp" /c:"Dashboard"
curl.exe -s http://127.0.0.1/market/index.php | findstr /i /c:"GMI" /c:"Market" /c:"vault" /c:"Omni"
