@echo off
powershell -NoProfile -Command "$roots=@('AORebirth\Built\Debug','AORebirth\Server\ZoneEngine'); foreach($root in $roots){ Get-ChildItem $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -match '\.(dat|bin|json|xml)$' -and $_.Name -match 'playfield|statel|Items|items' } | Select-Object -First 20 FullName,Length }"
