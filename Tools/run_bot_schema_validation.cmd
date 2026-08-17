@echo off
setlocal
if not "%~1"=="--run-disposable" goto :usage
if not "%~2"=="" goto :usage
if not "%AO_REBIRTH_ALLOW_DISPOSABLE_BOT_SCHEMA_VALIDATION%"=="1" (
    echo REFUSED: AO_REBIRTH_ALLOW_DISPOSABLE_BOT_SCHEMA_VALIDATION=1 is required.
    exit /b 2
)
set "SCRIPT_DIR=%~dp0"
set "PROJECT=%SCRIPT_DIR%..\Tools\BotSchemaValidation\BotSchemaValidation.csproj"
dotnet restore "%PROJECT%" --nologo
if errorlevel 1 exit /b %ERRORLEVEL%
dotnet build "%PROJECT%" --configuration Release --no-restore --nologo
if errorlevel 1 exit /b %ERRORLEVEL%
dotnet run --project "%PROJECT%" --configuration Release --no-build -- --run-disposable
exit /b %ERRORLEVEL%
:usage
echo Usage: run_bot_schema_validation.cmd --run-disposable
echo Requires AO_REBIRTH_ALLOW_DISPOSABLE_BOT_SCHEMA_VALIDATION=1.
exit /b 2
