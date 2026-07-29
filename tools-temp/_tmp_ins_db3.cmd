@echo off
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT aoid, name FROM itemnames WHERE name LIKE '%%nsurance%%' OR name LIKE '%%Save Char%%' OR name LIKE '%%save character%%' LIMIT 40;"
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT playfield, type, instance FROM staticdynels WHERE playfield IN (655, 545, 505, 550) AND type=51005 LIMIT 5;"
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "DESCRIBE staticdynels;"
