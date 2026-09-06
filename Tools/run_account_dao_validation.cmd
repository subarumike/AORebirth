@echo off
setlocal
if not "%~1"=="" exit /b 2
set "AO_REBIRTH_ALLOW_DISPOSABLE_ACCOUNT_DAO_VALIDATION=1"
dotnet run --project "%~dp0AccountDaoValidation\AccountDaoValidation.csproj" --configuration Release -- --run-disposable
exit /b %errorlevel%
