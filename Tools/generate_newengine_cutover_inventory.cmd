@echo off
setlocal
if not "%~1"=="--write" if not "%~1"=="--check" exit /b 64
if not "%~2"=="" exit /b 64
dotnet run --project "%~dp0NewEngineCutoverInventory\NewEngineCutoverInventory.csproj" --configuration Release -- %1 "%~dp0.."
exit /b %errorlevel%
