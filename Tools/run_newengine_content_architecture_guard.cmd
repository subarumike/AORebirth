@echo off
setlocal
if not "%~1"=="--write" if not "%~1"=="--check" if not "%~1"=="--baseline" if not "%~1"=="--self-test" if not "%~1"=="--enforce" if not "%~1"=="--doa-self-test" exit /b 64
dotnet run --project "%~dp0NewEngineRuntimeContentGuard\NewEngineRuntimeContentGuard.csproj" --configuration Release -- %1 "%~dp0.." %2 %3 %4 %5 %6
exit /b %errorlevel%
