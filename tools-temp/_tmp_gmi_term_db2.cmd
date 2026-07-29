@echo off
C:\xampp\mysql\bin\mysql.exe -uroot cellao_codex_clean -e "SHOW COLUMNS FROM staticdynels; SELECT * FROM staticdynels WHERE id LIKE '%%9F20%%' OR HEX(id) LIKE '%%9F20%%' LIMIT 5;"
powershell -NoProfile -Command "[Convert]::ToString(([int64]-1073282272 -band 0xffffffff),16)"
