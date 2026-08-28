@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

python -m unittest Tools.tests.test_npc_identity_bridge_replay
if errorlevel 1 goto :fail

python Tools\npc_identity_bridge_replay.py %*
if errorlevel 1 goto :fail

popd
exit /b 0

:fail
set "NPC_IDENTITY_BRIDGE_REPLAY_EXIT=%errorlevel%"
popd
exit /b %NPC_IDENTITY_BRIDGE_REPLAY_EXIT%
