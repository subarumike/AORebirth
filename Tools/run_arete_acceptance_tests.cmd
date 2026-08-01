@echo off
setlocal

set "RUNNER=%~dp0run_aotomation_messaging_tests.cmd"

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~Arete"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatProfileCatalogTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatActiveCoverageTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests"
if errorlevel 1 exit /b %errorlevel%

exit /b 0
