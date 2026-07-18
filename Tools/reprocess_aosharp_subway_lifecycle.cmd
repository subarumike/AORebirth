@echo off
setlocal
python "%~dp0reprocess_aosharp_subway_lifecycle.py" %*
exit /b %ERRORLEVEL%
