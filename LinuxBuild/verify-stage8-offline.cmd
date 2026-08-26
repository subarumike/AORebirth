@echo off
setlocal
pushd "%~dp0.." || exit /b 1
for /f "usebackq delims=" %%I in (`git rev-parse HEAD`) do set SOURCE_SHA=%%I
if "%SOURCE_SHA%"=="" exit /b 1
dotnet build LinuxBuild\Tools\Stage8OfflineSmokeTests\Stage8OfflineSmokeTests.csproj -c Release -v:minimal || exit /b 1
dotnet LinuxBuild\Tools\Stage8OfflineSmokeTests\bin\Release\net10.0\Stage8OfflineSmokeTests.dll --repository-root . --zone-output LinuxBuild\Projects\bin\ZoneEngine.Linux\Release\net10.0 --source-sha "%SOURCE_SHA%" --build-platform windows-hosted-linux-publish || exit /b 1
popd
endlocal
