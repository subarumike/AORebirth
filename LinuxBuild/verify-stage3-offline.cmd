@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "TEST_PROJECT=%SCRIPT_DIR%Tools\Stage3OfflineSmokeTests\Stage3OfflineSmokeTests.csproj"
set "DATABASE_PROJECT=%SCRIPT_DIR%Projects\AORebirth.Database.Linux.csproj"
set "CONTENT_MANIFEST=%SCRIPT_DIR%source-inventory\AORebirth.Database.ContentItems.props"
set "SOURCE_SQL=%SCRIPT_DIR%..\AORebirth\Libraries\Source\AORebirth.Database\SqlTables"
set "BUILD_OUTPUT=%SCRIPT_DIR%Tools\Stage3OfflineSmokeTests\bin\stage3-database-build"
set "PUBLISH_OUTPUT=%SCRIPT_DIR%Tools\Stage3OfflineSmokeTests\bin\stage3-database-linux-x64-publish"

pushd "%SCRIPT_DIR%" || exit /b 1

dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 goto :fail

dotnet run --project "%TEST_PROJECT%" --configuration Release
if errorlevel 1 goto :fail

dotnet build "%DATABASE_PROJECT%" --configuration Release --nologo --output "%BUILD_OUTPUT%"
if errorlevel 1 goto :fail

dotnet publish "%DATABASE_PROJECT%" --configuration Release --runtime linux-x64 --self-contained false --nologo --output "%PUBLISH_OUTPUT%"
if errorlevel 1 goto :fail

dotnet run --project "%TEST_PROJECT%" --configuration Release --no-build -- verify-artifacts "%SOURCE_SQL%" "%BUILD_OUTPUT%\SqlTables" "%PUBLISH_OUTPUT%\SqlTables" "%DATABASE_PROJECT%" "%CONTENT_MANIFEST%"
if errorlevel 1 goto :fail

popd
exit /b 0

:fail
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
