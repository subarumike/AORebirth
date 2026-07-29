@echo off
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "DESCRIBE itemnames; SELECT * FROM itemnames WHERE name LIKE '%%nsurance%%' LIMIT 40;"
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT Id, Playfield, Type, Instance, LENGTH(customevents) AS evlen, LENGTH(stats) AS stlen FROM staticdynels WHERE Playfield=655 AND Type=51005 AND LENGTH(customevents)>0 LIMIT 30;"
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT Id, Playfield, Type, Instance, LENGTH(customevents) AS evlen FROM staticdynels WHERE LENGTH(customevents)>100 LIMIT 20;"
