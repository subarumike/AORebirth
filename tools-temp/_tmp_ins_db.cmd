@echo off
mysql -uroot cellao_codex_clean -e "SHOW TABLES LIKE '%%statel%%'; SHOW TABLES LIKE '%%static%%'; SHOW TABLES LIKE '%%playfield%%';"
