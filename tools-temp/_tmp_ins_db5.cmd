@echo off
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT Id, Name, ItemType FROM itemnames WHERE Id BETWEEN 261415 AND 261424;"
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT COUNT(*) AS with_events FROM staticdynels WHERE customevents IS NOT NULL AND LENGTH(customevents)>0;"
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SELECT Playfield, Type, Instance, LENGTH(customevents) ev FROM staticdynels WHERE Playfield IN (545,655,505,550,800) AND Type=51005 AND customevents IS NOT NULL AND LENGTH(customevents)>0 LIMIT 40;"
