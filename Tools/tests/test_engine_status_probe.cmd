@echo off
setlocal EnableExtensions DisableDelayedExpansion

call "%~dp0..\..\status-engines.cmd" --self-test
set "TEST_EXIT=%ERRORLEVEL%"
exit /b %TEST_EXIT%
