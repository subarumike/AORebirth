@echo off
setlocal EnableExtensions

set "REPO=%~dp0"
set "LOCAL_ENV=%REPO%\AORebirth-local.env.cmd"
set "ENGINE_DIR=%REPO%\AORebirth\Built\Debug"
set "LOGIN_EXE=%ENGINE_DIR%\LoginEngine.exe"
set "ZONE_EXE=%ENGINE_DIR%\ZoneEngine.exe"
set "LOG_DIR=%REPO%\logs\engines"

cd /d "%REPO%"
if errorlevel 1 (
    echo [Rebuild-ZoneEngine] Failed to switch to repo: %REPO%
    pause
    exit /b 1
)

if not exist "%LOCAL_ENV%" (
    echo [Rebuild-ZoneEngine] Missing local env: %LOCAL_ENV%
    echo Create AORebirth-local.env.cmd on your desktop with AO_REBIRTH_MYSQL_CONNECTION set.
    pause
    exit /b 1
)

call "%LOCAL_ENV%"
if errorlevel 1 (
    echo [Rebuild-ZoneEngine] Local env script failed: %LOCAL_ENV%
    pause
    exit /b 1
)

if "%AO_REBIRTH_MYSQL_CONNECTION%"=="" (
    echo [Rebuild-ZoneEngine] AO_REBIRTH_MYSQL_CONNECTION is not set.
    echo Edit %LOCAL_ENV% and set your MySQL connection string.
    pause
    exit /b 1
)

echo %AO_REBIRTH_MYSQL_CONNECTION% | findstr /I /C:"YOUR_PASSWORD_HERE" /C:"REPLACE_WITH" >nul
if not errorlevel 1 (
    echo [Rebuild-ZoneEngine] MySQL password still looks like a placeholder.
    echo Edit %LOCAL_ENV% with your real local MySQL credentials.
    pause
    exit /b 1
)

echo.
echo [1/3] Stopping existing LoginEngine and ZoneEngine...
call "%REPO%\stop-engines.cmd" -EngineName LoginEngine
call "%REPO%\stop-engines.cmd" -EngineName ZoneEngine
rem Fallback if engines were started outside the managed launcher (or stop left a lock).
taskkill /F /IM LoginEngine.exe >nul 2>&1
taskkill /F /IM ZoneEngine.exe >nul 2>&1

echo.
echo [2/3] Building AORebirth Debug (includes LoginEngine and ZoneEngine)...
call "%REPO%\tools\build_aorebirth_debug.cmd"
if errorlevel 1 (
    echo [Rebuild-ZoneEngine] Build failed.
    pause
    exit /b 1
)

if not exist "%LOGIN_EXE%" (
    echo [Rebuild-ZoneEngine] Missing after build: %LOGIN_EXE%
    pause
    exit /b 1
)
if not exist "%ZONE_EXE%" (
    echo [Rebuild-ZoneEngine] Missing after build: %ZONE_EXE%
    pause
    exit /b 1
)

echo.
echo [3/3] Launching freshly built LoginEngine and ZoneEngine...
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$wd = $env:ENGINE_DIR; $logDir = $env:LOG_DIR; if ([string]::IsNullOrWhiteSpace($env:AO_REBIRTH_MYSQL_CONNECTION)) { throw 'AO_REBIRTH_MYSQL_CONNECTION was not inherited into the launcher.' }; function Start-ManagedEngine([string]$Name, [string]$Exe, [int[]]$Ports) { $shutdown = Join-Path $logDir ($Name + '.shutdown'); $pidFile = Join-Path $logDir ($Name + '.pid.json'); if (Test-Path $shutdown) { Remove-Item -LiteralPath $shutdown -Force }; $engineArgs = @('/autostart', '/shutdown-file', $shutdown); $p = Start-Process -FilePath $Exe -ArgumentList $engineArgs -WorkingDirectory $wd -WindowStyle Normal -PassThru; Start-Sleep -Milliseconds 250; $p.Refresh(); if ($p.HasExited) { throw ($Name + ' exited immediately.') }; @{ Engine = $Name; Pid = $p.Id; Path = $Exe; StartedAt = (Get-Date).ToString('o'); Visible = $true; Arguments = $engineArgs; Ports = $Ports; StandardOutput = $null; StandardError = $null; ShutdownFile = $shutdown } | ConvertTo-Json -Depth 4 | Set-Content -Path $pidFile -Encoding UTF8; Write-Host ($Name + ' started pid=' + $p.Id) }; Start-ManagedEngine 'LoginEngine' $env:LOGIN_EXE @(7500); Start-ManagedEngine 'ZoneEngine' $env:ZONE_EXE @(7501)"
if errorlevel 1 (
    echo [Rebuild-ZoneEngine] Launch failed.
    pause
    exit /b 1
)

echo.
echo [Rebuild-ZoneEngine] Done.
pause
exit /b 0
