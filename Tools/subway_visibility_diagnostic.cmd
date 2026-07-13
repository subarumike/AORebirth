@echo off
setlocal
python "%~dp0subway_visibility_diagnostic.py" %*
exit /b %ERRORLEVEL%
