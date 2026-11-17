@echo off
rem Starts the PORK exporter from this folder. Extra arguments: run (default), sweep or check.
cd /d "%~dp0"
php pork.php %*
