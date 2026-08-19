@echo off
call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
%AO_REBIRTH_PYTHON% -m unittest discover -s "%~dp0tests" -p "test_inventory_aosharp_captures.py"
exit /b %errorlevel%
