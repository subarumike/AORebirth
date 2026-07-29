@echo off
echo === sizes ===
dir C:\xampp\htdocs\index.app
dir C:\xampp\htdocs\market\index.app
echo === first lines root index.app ===
powershell -NoProfile -Command "Get-Content 'C:\xampp\htdocs\index.app' -TotalCount 30 -ErrorAction SilentlyContinue"
echo === first lines market index.app ===
powershell -NoProfile -Command "Get-Content 'C:\xampp\htdocs\market\index.app' -TotalCount 30 -ErrorAction SilentlyContinue"
echo === market index.php ===
powershell -NoProfile -Command "Get-Content 'C:\xampp\htdocs\market\index.php' -TotalCount 20"
echo === tools-temp gmi index ===
if exist "C:\Users\nermi\source\repos\AORebirth\tools-temp\gmi-local-web\index.app" (echo HAS tools-temp index.app) else (echo NO tools-temp index.app)
dir /b "C:\Users\nermi\source\repos\AORebirth\tools-temp\gmi-local-web\index.*"
