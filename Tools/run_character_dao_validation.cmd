@echo off
setlocal
if not "%~1"=="" exit /b 2
cd /d "%~dp0.." || exit /b 1
set "AO_REBIRTH_ALLOW_DISPOSABLE_CHARACTER_DAO_VALIDATION=1"
dotnet run --project "%~dp0CharacterDaoValidation\CharacterDaoValidation.csproj" --configuration Release -- --run-disposable
exit /b %ERRORLEVEL%
