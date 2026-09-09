@echo off
setlocal
pushd "%~dp0.."
dotnet run --project Tools\AcceptedOrdinaryBindingExport\AcceptedOrdinaryBindingExport.csproj --configuration Release -- %* "%CD%"
set "TASK_EXIT=%ERRORLEVEL%"
popd
exit /b %TASK_EXIT%
