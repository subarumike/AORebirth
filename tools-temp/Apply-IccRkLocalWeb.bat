@echo off
:: Deploy Item Store + Daily like GMI market.
:: In-game Awesomium does NOT fetch the VGTP hostname for content.
:: It fetches Funcom HTTP URLs from GUI.dll:
::   http://aoshop.funcom.com/shop/
::   http://dailyrewards.anarchy-online.com/
::   http://aomarket.funcom.com/market/   (already working GMI)
setlocal
python "C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_apply_aoshop_dailyrewards.py"
if errorlevel 1 exit /b 1
echo.
echo Hosts needed (script adds if missing):
echo   127.0.0.1 aoshop.funcom.com
echo   127.0.0.1 dailyrewards.anarchy-online.com
echo   127.0.0.1 aomarket.funcom.com
echo Folders:
echo   C:\xampp\htdocs\shop    (Item Store)
echo   C:\xampp\htdocs\daily   (Daily Rewards)
echo   C:\xampp\htdocs\market  (GMI)
echo Fully restart AO client, then reopen Item Store / Daily.
endlocal
