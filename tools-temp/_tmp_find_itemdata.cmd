@echo off
powershell -NoProfile -Command "$roots=@('AORebirth\Built\Debug','AORebirth\Server\ZoneEngine','tools-temp'); foreach($r in $roots){ if(Test-Path $r){ Get-ChildItem $r -Recurse -Include *.dat,*.bin,*.xml,*.json,*.txt -ErrorAction SilentlyContinue | Where-Object { $_.Length -lt 80MB -and $_.Name -match 'item|statel|playfield|pfdata|4680' } | Select-Object -First 40 FullName,Length } }"
