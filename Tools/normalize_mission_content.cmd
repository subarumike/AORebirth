@echo off
setlocal
dotnet run --project "%~dp0MissionContentNormalizer\MissionContentNormalizer.csproj" --configuration Release -- %*
exit /b %errorlevel%
