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

for /f "usebackq delims=" %%I in (`git -C .. rev-parse HEAD`) do set SOURCE_SHA=%%I
if "%SOURCE_SHA%"=="" goto :failed

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
  dotnet Tools\Stage8OfflineSmokeTests\bin\Release\net10.0\Stage8OfflineSmokeTests.dll --repository-root .. --zone-output "artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%" --source-sha "%SOURCE_SHA%" --build-platform windows-hosted-linux-publish --structure-only
) else (
  dotnet Tools\Stage8OfflineSmokeTests\bin\Release\net10.0\Stage8OfflineSmokeTests.dll --repository-root .. --zone-output "artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%" --source-sha "%SOURCE_SHA%" --build-platform windows-hosted-linux-publish
)
if errorlevel 1 goto :failed

for /f "usebackq delims=" %%I in (`dotnet --version`) do set DOTNET_SDK_VERSION=%%I
set TRACKED_SOURCE_CLEAN=PASS
git -C .. diff --quiet --
if errorlevel 1 set TRACKED_SOURCE_CLEAN=FAIL
git -C .. diff --cached --quiet --
if errorlevel 1 set TRACKED_SOURCE_CLEAN=FAIL
set PUBLISH_DIR=artifacts\zoneengine\%RUNTIME_ID%\%PACKAGE_KIND%
set PLACEMENT_DIR=%PUBLISH_DIR%\Content\Official\PlayfieldPlacements
set PLACEMENT_BUILD_MANIFEST=%PLACEMENT_DIR%\official-placement-build-manifest.json
set PLACEMENT_PROVENANCE=%PLACEMENT_DIR%\PLACEMENT_PROVENANCE.env
if not exist "%PLACEMENT_BUILD_MANIFEST%" goto :failed
if not exist "%PLACEMENT_PROVENANCE%" goto :failed
set PLACEMENT_SOURCE_SHA=
set PLACEMENT_BUILD_PLATFORM=
for /f "usebackq tokens=1,* delims==" %%A in ("%PLACEMENT_PROVENANCE%") do (
  if "%%A"=="SOURCE_SHA" set PLACEMENT_SOURCE_SHA=%%B
  if "%%A"=="BUILD_PLATFORM" set PLACEMENT_BUILD_PLATFORM=%%B
  if "%%A"=="PLACEMENT_CORPUS_VERSION" set PLACEMENT_CORPUS_VERSION=%%B
  if "%%A"=="PLACEMENT_CORPUS_MANIFEST_SHA256" set PLACEMENT_CORPUS_MANIFEST_SHA256=%%B
  if "%%A"=="PLACEMENT_CORPUS_SUMMARY_SHA256" set PLACEMENT_CORPUS_SUMMARY_SHA256=%%B
  if "%%A"=="PLACEMENT_CORPUS_INDEX_SHA256" set PLACEMENT_CORPUS_INDEX_SHA256=%%B
  if "%%A"=="PLACEMENT_ACGHASH_INVENTORY_SHA256" set PLACEMENT_ACGHASH_INVENTORY_SHA256=%%B
  if "%%A"=="PLACEMENT_BUILD_MANIFEST_SHA256" set PLACEMENT_BUILD_MANIFEST_SHA256=%%B
  if "%%A"=="PLACEMENT_RESOURCE_COUNT" set PLACEMENT_RESOURCE_COUNT=%%B
  if "%%A"=="PLACEMENT_PARSED_RESOURCE_COUNT" set PLACEMENT_PARSED_RESOURCE_COUNT=%%B
  if "%%A"=="PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT" set PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=%%B
  if "%%A"=="PLACEMENT_DISTRICT_COUNT" set PLACEMENT_DISTRICT_COUNT=%%B
  if "%%A"=="PLACEMENT_RECORD_COUNT" set PLACEMENT_RECORD_COUNT=%%B
  if "%%A"=="PLACEMENT_UNIQUE_ACGHASH_COUNT" set PLACEMENT_UNIQUE_ACGHASH_COUNT=%%B
  if "%%A"=="PLACEMENT_RUNTIME_AUTHORIZED_COUNT" set PLACEMENT_RUNTIME_AUTHORIZED_COUNT=%%B
)
if not "%PLACEMENT_SOURCE_SHA%"=="%SOURCE_SHA%" goto :failed
if not "%PLACEMENT_BUILD_PLATFORM%"=="windows-hosted-linux-publish" goto :failed
if not "%PLACEMENT_RESOURCE_COUNT%"=="630" goto :failed
if not "%PLACEMENT_PARSED_RESOURCE_COUNT%"=="627" goto :failed
if not "%PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT%"=="3" goto :failed
if not "%PLACEMENT_DISTRICT_COUNT%"=="4146" goto :failed
if not "%PLACEMENT_RECORD_COUNT%"=="32805" goto :failed
if not "%PLACEMENT_UNIQUE_ACGHASH_COUNT%"=="4016" goto :failed
if not "%PLACEMENT_RUNTIME_AUTHORIZED_COUNT%"=="25" goto :failed
if "%PLACEMENT_CORPUS_VERSION%"=="" goto :failed
if "%PLACEMENT_CORPUS_MANIFEST_SHA256%"=="" goto :failed
if "%PLACEMENT_CORPUS_SUMMARY_SHA256%"=="" goto :failed
if "%PLACEMENT_CORPUS_INDEX_SHA256%"=="" goto :failed
if "%PLACEMENT_ACGHASH_INVENTORY_SHA256%"=="" goto :failed
if "%PLACEMENT_BUILD_MANIFEST_SHA256%"=="" goto :failed
> "%PUBLISH_DIR%\SOURCE_SHA" echo %SOURCE_SHA%
> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo REPOSITORY=AORebirth
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo COMMIT_SHA=%SOURCE_SHA%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo BUILD_PLATFORM=windows-hosted-linux-publish
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo RUNTIME_IDENTIFIER=%RUNTIME_ID%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo CONFIGURATION=Release
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo SELF_CONTAINED=%SELF_CONTAINED%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo DOTNET_SDK_VERSION=%DOTNET_SDK_VERSION%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo TRACKED_SOURCE_CLEAN=%TRACKED_SOURCE_CLEAN%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo BUILD_TIMESTAMP_LOCAL=%DATE% %TIME%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo ACCEPTANCE_RESULT=UNVERIFIED
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_CORPUS_VERSION=%PLACEMENT_CORPUS_VERSION%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_CORPUS_MANIFEST_SHA256=%PLACEMENT_CORPUS_MANIFEST_SHA256%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_CORPUS_SUMMARY_SHA256=%PLACEMENT_CORPUS_SUMMARY_SHA256%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_CORPUS_INDEX_SHA256=%PLACEMENT_CORPUS_INDEX_SHA256%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_ACGHASH_INVENTORY_SHA256=%PLACEMENT_ACGHASH_INVENTORY_SHA256%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_BUILD_MANIFEST_SHA256=%PLACEMENT_BUILD_MANIFEST_SHA256%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_RESOURCE_COUNT=%PLACEMENT_RESOURCE_COUNT%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_PARSED_RESOURCE_COUNT=%PLACEMENT_PARSED_RESOURCE_COUNT%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=%PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_DISTRICT_COUNT=%PLACEMENT_DISTRICT_COUNT%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_RECORD_COUNT=%PLACEMENT_RECORD_COUNT%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_UNIQUE_ACGHASH_COUNT=%PLACEMENT_UNIQUE_ACGHASH_COUNT%
>> "%PUBLISH_DIR%\BUILD_PROVENANCE.env" echo PLACEMENT_RUNTIME_AUTHORIZED_COUNT=%PLACEMENT_RUNTIME_AUTHORIZED_COUNT%

popd
endlocal
exit /b 0

:failed
popd
endlocal
exit /b 1
