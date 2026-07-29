@echo off
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SHOW TABLES;"
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT playfield, type, instance, HEX(instance) AS hx FROM staticdynels WHERE instance=-1073347953 OR playfield=655 LIMIT 20;"
