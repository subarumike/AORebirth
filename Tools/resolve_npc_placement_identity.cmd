@echo off
setlocal
pushd "%~dp0.."
if errorlevel 1 exit /b 1

python -m unittest Tools.tests.test_npc_placement_identity_resolver
if errorlevel 1 goto :fail

python Tools\npc_placement_identity_resolver.py --tests-status PASS %*
if errorlevel 1 goto :fail

popd
exit /b 0

:fail
set "NPC_PLACEMENT_RESOLVER_EXIT=%errorlevel%"
popd
exit /b %NPC_PLACEMENT_RESOLVER_EXIT%
