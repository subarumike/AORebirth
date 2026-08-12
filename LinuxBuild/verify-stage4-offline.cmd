@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "TEST_PROJECT=%SCRIPT_DIR%Tools\Stage4OfflineSmokeTests\Stage4OfflineSmokeTests.csproj"
set "PUBLISH_OUTPUT=%SCRIPT_DIR%Tools\Stage4OfflineSmokeTests\bin\stage4-linux-x64-publish"
set "PUBLISH_DLL=%PUBLISH_OUTPUT%\Stage4OfflineSmokeTests.dll"

pushd "%SCRIPT_DIR%" || exit /b 1

dotnet run --project "%TEST_PROJECT%" --configuration Release -- source
if errorlevel 1 goto :fail

dotnet publish "%TEST_PROJECT%" --configuration Release --runtime linux-x64 --self-contained false --nologo -p:PublishTrimmed=false -p:PublishAot=false --output "%PUBLISH_OUTPUT%"
if errorlevel 1 goto :fail

dotnet "%PUBLISH_DLL%" published-linux-x64
if errorlevel 1 goto :fail

popd
exit /b 0

:fail
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%
