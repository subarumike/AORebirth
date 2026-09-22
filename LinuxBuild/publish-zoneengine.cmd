@echo off
setlocal
set RUNTIME_ID=%~1
if "%RUNTIME_ID%"=="" set RUNTIME_ID=linux-x64
if not "%~3"=="" (
  echo Engine selection is retired; NewEngine is the only publish target.
  exit /b 2
)
set ENGINE=new
set ZONE_PROJECT=..\AORebirth\Server\ZoneEngine_New\ZoneEngine_New.csproj
set ARTIFACT_NAME=zoneengine
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

for /f "usebackq delims=" %%I in (`git -C .. rev-parse HEAD`) do set SOURCE_SHA=%%I
if "%SOURCE_SHA%"=="" goto :failed

dotnet run --project Tools\SourceInventoryGuard\SourceInventoryGuard.csproj -- --repository-root .. --manifest source-inventory\inventory.json --check
if errorlevel 1 goto :failed

if exist "artifacts\%ARTIFACT_NAME%\%RUNTIME_ID%\%PACKAGE_KIND%" rmdir /s /q "artifacts\%ARTIFACT_NAME%\%RUNTIME_ID%\%PACKAGE_KIND%"
mkdir "artifacts\%ARTIFACT_NAME%\%RUNTIME_ID%\%PACKAGE_KIND%"
if errorlevel 1 goto :failed

dotnet restore "%ZONE_PROJECT%" --runtime "%RUNTIME_ID%" --nologo
if errorlevel 1 goto :failed

dotnet clean "%ZONE_PROJECT%" --configuration Release --runtime "%RUNTIME_ID%" --nologo
if errorlevel 1 goto :failed

dotnet publish "%ZONE_PROJECT%" --configuration Release --runtime "%RUNTIME_ID%" --self-contained "%SELF_CONTAINED%" --output "artifacts\%ARTIFACT_NAME%\%RUNTIME_ID%\%PACKAGE_KIND%" --no-restore --nologo
if errorlevel 1 goto :failed

dotnet run --project Tools\BackendIntegrationGuard\BackendIntegrationGuard.csproj --configuration Release -- --repository-root .. --publish "artifacts\%ARTIFACT_NAME%\%RUNTIME_ID%\%PACKAGE_KIND%" --source-sha "%SOURCE_SHA%" --build-platform windows-hosted-linux-publish --self-test
if errorlevel 1 goto :failed

:publish_validated
for /f "usebackq delims=" %%I in (`dotnet --version`) do set DOTNET_SDK_VERSION=%%I
set TRACKED_SOURCE_CLEAN=PASS
git -C .. diff --quiet --
if errorlevel 1 set TRACKED_SOURCE_CLEAN=FAIL
git -C .. diff --cached --quiet --
if errorlevel 1 set TRACKED_SOURCE_CLEAN=FAIL
set PUBLISH_DIR=artifacts\%ARTIFACT_NAME%\%RUNTIME_ID%\%PACKAGE_KIND%
:new_content_provenance
call ..\Tools\select_python_runtime.cmd
if errorlevel 1 goto :failed
%AO_REBIRTH_PYTHON% Tools\content_provenance.py write "%PUBLISH_DIR%" "%SOURCE_SHA%" windows-hosted-linux-publish > "%PUBLISH_DIR%\CONTENT_PROVENANCE.env"
if errorlevel 1 goto :failed

:write_common_provenance
> "%PUBLISH_DIR%\SOURCE_SHA" echo %SOURCE_SHA%
> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo REPOSITORY=AORebirth
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo COMMIT_SHA=%SOURCE_SHA%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo ZONEENGINE_IMPLEMENTATION=%ENGINE%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo BUILD_PLATFORM=windows-hosted-linux-publish
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo RUNTIME_IDENTIFIER=%RUNTIME_ID%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo CONFIGURATION=Release
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo SELF_CONTAINED=%SELF_CONTAINED%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo DOTNET_SDK_VERSION=%DOTNET_SDK_VERSION%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo TRACKED_SOURCE_CLEAN=%TRACKED_SOURCE_CLEAN%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo BUILD_TIMESTAMP_LOCAL=%DATE% %TIME%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo ACCEPTANCE_RESULT=UNVERIFIED
type "%PUBLISH_DIR%\CONTENT_PROVENANCE.env" >> "%PUBLISH_DIR%\BUILD_PROVENANCE.env"

:publish_done
popd
endlocal
exit /b 0

:failed
popd
endlocal
exit /b 1
