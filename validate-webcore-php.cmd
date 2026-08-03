@echo off
setlocal

set "BUILT_ROOT=%~dp0AORebirth\Built\Debug"
set "PHP_ROOT=%BUILT_ROOT%\php"
set "WEBCORE_ROOT=%BUILT_ROOT%\htdocs"
set "STATE_ROOT=%BUILT_ROOT%\WebEngineData"
set "LINT_LIST=%TEMP%\aorebirth-webcore-php-lint-%RANDOM%-%RANDOM%.txt"

call "%~dp0validate-php-runtime.cmd"
if errorlevel 1 exit /b 2

call "%~dp0validate-webcore-assets.cmd"
if errorlevel 1 exit /b 2

python "%~dp0Tools\webcore_php_compatibility.py" validate "%WEBCORE_ROOT%"
if errorlevel 1 exit /b 2

python "%~dp0Tools\webcore_php_compatibility.py" lint-list "%WEBCORE_ROOT%" > "%LINT_LIST%"
if errorlevel 1 (
    if exist "%LINT_LIST%" del /q "%LINT_LIST%"
    exit /b 2
)

set "PHPRC=%PHP_ROOT%\php.ini"
set "PHP_INI_SCAN_DIR="
set "AOREBIRTH_PHP_STATE_DIR=%STATE_ROOT%"
set "AOREBIRTH_WEBCORE_ROOT=%WEBCORE_ROOT%"

set "INI_REPORT=%STATE_ROOT%\php-ini-report-%RANDOM%-%RANDOM%.txt"
"%PHP_ROOT%\php.exe" -c "%PHP_ROOT%\php.ini" --ini >"%INI_REPORT%" 2>&1
if errorlevel 1 (
    del /q "%INI_REPORT%" >nul 2>&1
    echo [AORebirth WebCore PHP] PHP configuration discovery failed.
    exit /b 3
)
set "ADDITIONAL_INI="
for /f "tokens=1,* delims=:" %%A in ('findstr /b /c:"Additional .ini files parsed:" "%INI_REPORT%"') do set "ADDITIONAL_INI=%%B"
for /f "tokens=* delims= " %%A in ("%ADDITIONAL_INI%") do set "ADDITIONAL_INI=%%A"
if /i not "%ADDITIONAL_INI%"=="(none)" (
    del /q "%INI_REPORT%" >nul 2>&1
    echo [AORebirth WebCore PHP] Supplemental PHP INI scanning is not disabled.
    exit /b 3
)
del /q "%INI_REPORT%" >nul 2>&1
set "REDIRECT_STATUS=200"

for /f "usebackq delims=" %%F in ("%LINT_LIST%") do (
    "%PHP_ROOT%\php.exe" -c "%PHP_ROOT%\php.ini" -l "%%F" >nul
    if errorlevel 1 (
        del /q "%LINT_LIST%"
        echo [AORebirth WebCore PHP Validate] FAIL: %%F
        exit /b 3
    )
)

del /q "%LINT_LIST%"
echo [AORebirth WebCore PHP Validate] PASS: all patched PHP files linted.
exit /b 0
