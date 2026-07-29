@echo off
echo === hosts gmi/market ===
findstr /i /c:"gmi" /c:"market" /c:"omni" C:\Windows\System32\drivers\etc\hosts
echo === market web root ===
if exist C:\xampp\htdocs\market\index.php (echo HAS_index.php) else (echo MISSING_index.php)
if exist C:\xampp\htdocs\market\vault.php (echo HAS_vault.php) else (echo MISSING_vault.php)
if exist C:\xampp\htdocs\market\gmi_db_config.php (echo HAS_gmi_db_config.php) else (echo MISSING_gmi_db_config.php)
echo === last MarketSend log lines ===
findstr /c:"MarketSend" AORebirth\Built\Debug\ZoneEngineLog.txt | more +99999
echo === sample vault rows ===
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT character_id, character_name, credits FROM gmi_vault; SELECT character_id, low_id, quality, stack_count, item_name FROM gmi_vault_item LIMIT 10;"
