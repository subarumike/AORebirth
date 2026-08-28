@echo off
setlocal
cd /d "%~dp0.."
python -m unittest Tools.tests.test_acg_placement_schema_audit || exit /b 1
python Tools\acg_placement_schema_audit.py %* || exit /b 1
echo TESTS=PASS
exit /b 0
