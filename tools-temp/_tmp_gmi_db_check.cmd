@echo off
set MYSQL=C:\xampp\mysql\bin\mysql.exe
if not exist "%MYSQL%" (
  echo NO_MYSQL
  exit /b 1
)
"%MYSQL%" -uroot cellao_codex_clean -e "SHOW TABLES LIKE 'gmi%%'; SELECT COUNT(*) AS vaults FROM gmi_vault; SELECT COUNT(*) AS items FROM gmi_vault_item;"
