@echo off
setlocal
set RUNTIME_ID=%~1
if "%RUNTIME_ID%"=="" set RUNTIME_ID=linux-x64
set SELF_CONTAINED=%~2
if "%SELF_CONTAINED%"=="" set SELF_CONTAINED=false

if "%RUNTIME_ID%"=="linux-x64" goto :runtime_ok
if "%RUNTIME_ID%"=="linux-arm64" goto :runtime_ok
exit /b 2
:runtime_ok

if "%SELF_CONTAINED%"=="true" (
  set PACKAGE_KIND=self-contained
) else if "%SELF_CONTAINED%"=="false" (
  set PACKAGE_KIND=framework-dependent
) else (
  exit /b 2
)

pushd "%~dp0" || exit /b 1

dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 goto :failed

if exist "artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%" rmdir /s /q "artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%"
mkdir "artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%"
if errorlevel 1 goto :failed

dotnet restore Projects\ZoneEngine.Linux.csproj --runtime "%RUNTIME_ID%" --nologo
if errorlevel 1 goto :failed

dotnet clean Projects\ZoneEngine.Linux.csproj --configuration Release --runtime "%RUNTIME_ID%" --nologo
if errorlevel 1 goto :failed

dotnet publish Projects\ZoneEngine.Linux.csproj --configuration Release --runtime "%RUNTIME_ID%" --self-contained "%SELF_CONTAINED%" --output "artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%" --no-restore --nologo
if errorlevel 1 goto :failed

dotnet build Tools\Stage8OfflineSmokeTests\Stage8OfflineSmokeTests.csproj -c Release -v:minimal
if errorlevel 1 goto :failed

if "%SELF_CONTAINED%"=="true" (
  dotnet Tools\Stage8OfflineSmokeTests\bin\Release\net10.0\Stage8OfflineSmokeTests.dll --repository-root .. --zone-output "artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%" --structure-only
) else (
  dotnet Tools\Stage8OfflineSmokeTests\bin\Release\net10.0\Stage8OfflineSmokeTests.dll --repository-root .. --zone-output "artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%"
)
if errorlevel 1 goto :failed

popd
endlocal
exit /b 0

:failed
popd
endlocal
exit /b 1
