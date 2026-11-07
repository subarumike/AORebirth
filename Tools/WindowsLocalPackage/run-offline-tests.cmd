@echo off
setlocal EnableExtensions
if "%~2"=="" (
    echo Usage: run-offline-tests.cmd ^<launcher.exe^> ^<DatabaseSetup.dll^> [dotnet.exe]
    exit /b 2
)
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not "%~3"=="" set "DOTNET=%~f3"
set "LAUNCHER=%~f1"
set "DATABASE_SETUP=%~f2"
set "REPO=%~dp0..\.."
for %%I in ("%REPO%") do set "REPO=%%~fI"
pushd "%REPO%"
if errorlevel 1 exit /b 1
rem The harness opens synthetic files and migration resources only. It never starts game engines or MySQL.
call "%REPO%\tools\windows_release_contract.cmd" run-feature-validation -- "%ProgramFiles%\dotnet\dotnet.exe" run --project Tools/WindowsLocalPackage/Tests/WindowsLocalPackageValidation/WindowsLocalPackageValidation.csproj --configuration Release -- --launcher "%LAUNCHER%" --database-setup "%DATABASE_SETUP%" --dotnet "%DOTNET%"
set "TEST_RESULT=%errorlevel%"
popd
exit /b %TEST_RESULT%
