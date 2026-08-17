@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "RUNTIME_ID=%~1"
set "SELF_CONTAINED=%~2"
if "%RUNTIME_ID%"=="" set "RUNTIME_ID=linux-x64"
if "%SELF_CONTAINED%"=="" set "SELF_CONTAINED=false"
if /i not "%RUNTIME_ID%"=="linux-x64" if /i not "%RUNTIME_ID%"=="linux-arm64" exit /b 2
if /i "%SELF_CONTAINED%"=="true" (
    set "PACKAGE_KIND=self-contained"
) else if /i "%SELF_CONTAINED%"=="false" (
    set "PACKAGE_KIND=framework-dependent"
) else (
    exit /b 2
)
pushd "%SCRIPT_DIR%" || exit /b 1
dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 goto :failed
dotnet restore Projects\BotServiceHost.Linux.csproj --runtime "%RUNTIME_ID%" --nologo
if errorlevel 1 goto :failed
dotnet clean Projects\BotServiceHost.Linux.csproj --configuration Release --runtime "%RUNTIME_ID%" --nologo
if errorlevel 1 goto :failed
dotnet publish Projects\BotServiceHost.Linux.csproj --configuration Release --runtime "%RUNTIME_ID%" --self-contained "%SELF_CONTAINED%" --output "artifacts\botservice\%RUNTIME_ID%\%PACKAGE_KIND%" --no-restore --nologo
if errorlevel 1 goto :failed
popd
exit /b 0
:failed
popd
exit /b 1
