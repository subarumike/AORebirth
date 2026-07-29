@echo off
powershell -NoProfile -Command "$id=-1073282272; $pf=4680; Write-Host ('looking for instance '+$id+' hex '+('{0:X8}' -f ([uint32]$id))); Select-String -Path 'AORebirth\Built\Debug\ZoneEngineLog.txt' -Pattern 'C0070320|OpenSystem|53168|FunctionType|CallFunction|missing function|unknown function|GMI|SystemDialog' | Select-Object -Last 30 | ForEach-Object { $_.Line }"
