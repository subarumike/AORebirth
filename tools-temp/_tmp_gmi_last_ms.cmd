@echo off
powershell -NoProfile -Command "$lines = Select-String -Path 'AORebirth\Built\Debug\ZoneEngineLog.txt' -Pattern 'MarketSend' | Select-Object -Last 15; $lines | ForEach-Object { $_.Line }"
