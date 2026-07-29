@echo off
powershell -NoProfile -Command "Select-String -Path 'AORebirth\Built\Debug\ZoneEngineLog.txt' -Pattern 'GenericCmd|Use Terminal|StaticDynel|OnUse|GMI|Market|socialstatus|SocialStatus' | Select-Object -Last 40 | ForEach-Object { $_.Line }"
