@echo off
setlocal
pushd "%~dp0.." || exit /b 1

call LinuxBuild\publish-zoneengine.cmd linux-x64 true
if errorlevel 1 goto :failed

if not exist LinuxBuild\deployment\systemd\ao-rebirth-zoneengine.service goto :failed
if not exist LinuxBuild\deployment\systemd\zoneengine.env.example goto :failed
if not exist LinuxBuild\deployment\zone-stage9\validate-disabled-service.sh goto :failed

if "%AO_REBIRTH_STAGE9_SSH_TARGET%"=="" (
  echo AO_REBIRTH_STAGE9_SSH_TARGET is required for native Stage 9 validation. 1>&2
  goto :failed
)

ssh -o BatchMode=yes "%AO_REBIRTH_STAGE9_SSH_TARGET%" "uname -a; printf ARCH=; uname -m; systemctl --version | sed -n '1p'"
if errorlevel 1 goto :failed

ssh -o BatchMode=yes "%AO_REBIRTH_STAGE9_SSH_TARGET%" "test -x /opt/ao-rebirth/zoneengine/current/ZoneEngine && test -f /etc/systemd/system/ao-rebirth-zoneengine.service"
if errorlevel 1 goto :failed

ssh -o BatchMode=yes "%AO_REBIRTH_STAGE9_SSH_TARGET%" "test \"$(systemctl is-active ao-rebirth-zoneengine.service)\" = inactive && test \"$(systemctl is-enabled ao-rebirth-zoneengine.service)\" != enabled"
if errorlevel 1 goto :failed

ssh -o BatchMode=yes "%AO_REBIRTH_STAGE9_SSH_TARGET%" "journalctl -u ao-rebirth-zoneengine.service -n 200 --no-pager | grep -q ZONEENGINE_DATABASE_OK"
if errorlevel 1 goto :failed

popd
endlocal
exit /b 0

:failed
popd
endlocal
exit /b 1
