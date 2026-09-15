@echo off
setlocal
if not "%~1"=="--write" if not "%~1"=="--check" if not "%~1"=="--baseline" if not "%~1"=="--self-test" exit /b 64
dotnet run --project "%~dp0NewEngineRuntimeContentGuard\NewEngineRuntimeContentGuard.csproj" --configuration Release -- %1 "%~dp0.." %2
exit /b %errorlevel%
