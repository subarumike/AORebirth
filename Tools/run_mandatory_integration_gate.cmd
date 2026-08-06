@echo off
setlocal EnableExtensions

call "%~dp0select_python_runtime.cmd"
if errorlevel 1 exit /b 1
if not "%AO_REBIRTH_GENERATED_COMBAT_LEASE_DELEGATION%"=="" (
    %AO_REBIRTH_PYTHON% "%~dp0generated_combat_pipeline.py" --_validate-read-delegation
    if errorlevel 1 exit /b 1
    goto :generated_combat_read_lease_acquired
)
%AO_REBIRTH_PYTHON% "%~dp0generated_combat_pipeline.py" --run-read-lease --read-lease-command-timeout-seconds 14400 -- "%ComSpec%" /d /c "%~f0" %*
exit /b %errorlevel%

:generated_combat_read_lease_acquired

pushd "%~dp0.." >nul
if errorlevel 1 (
    echo [AORebirth Gate] FAIL - cannot enter repository root.
    exit /b 1
)

for %%F in (
    "tools\scan_secrets.cmd"
    "tools\select_python_runtime.cmd"
    "tools\generate_capture_backed_npc_combat_inventory.cmd"
    "tools\generated_artifact_transaction.py"
    "tools\generated_combat_pipeline.py"
    "tools\run_generated_combat_concurrency_tests.cmd"
    "tools\stress_generated_combat_pipeline.py"
    "tools\tests\test_generated_artifact_transaction.py"
    "tools\tests\test_generated_combat_pipeline.py"
    "tools\run_aotomation_messaging_tests.cmd"
    "tools\run_engine_management_tests.cmd"
    "tools\run_web_engine_security_tests.cmd"
    "tools\run_subway_acceptance_tests.cmd"
    "tools\run_temple_acceptance_tests.cmd"
    "tools\generate_mission_level_graph.cmd"
    "tools\build_aorebirth_debug.cmd"
) do (
    if not exist "%%~F" (
        echo [AORebirth Gate] FAIL - required file is missing: %%~F
        goto :fail
    )
)
git lfs version >nul 2>nul
if errorlevel 1 (
    echo [AORebirth Gate] FAIL - Git LFS is unavailable.
    goto :fail
)

set "CURRENT_STAGE=1/11 secret scan"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\scan_secrets.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 1/11 secret scan

set "CURRENT_STAGE=2/11 engine management safety contracts"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_engine_management_tests.cmd --skip-web-engine-security
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 2/11 engine management safety contracts

set "CURRENT_STAGE=3/11 generated artifact reproducibility"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_generated_combat_concurrency_tests.cmd
if errorlevel 1 goto :stage_fail
call tools\generate_capture_backed_npc_combat_inventory.cmd --check
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 3/11 generated artifact reproducibility

set "CURRENT_STAGE=4/11 complete AOtomation suite"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_aotomation_messaging_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 4/11 complete AOtomation suite

set "CURRENT_STAGE=5/11 Subway acceptance"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_subway_acceptance_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 5/11 Subway acceptance

set "CURRENT_STAGE=6/11 Temple acceptance"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_temple_acceptance_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 6/11 Temple acceptance

set "CURRENT_STAGE=7/11 mission graph and generated mission reproducibility"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\generate_mission_level_graph.cmd --check
if errorlevel 1 goto :stage_fail
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~Mission"
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 7/11 mission graph and generated mission reproducibility

set "CURRENT_STAGE=8/11 Git LFS integrity"
echo [AORebirth Gate] START %CURRENT_STAGE%
git lfs fsck
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 8/11 Git LFS integrity

set "CURRENT_STAGE=9/11 debug server build"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\build_aorebirth_debug.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 9/11 debug server build

set "CURRENT_STAGE=10/11 offline PHP/WebCore compatibility"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_web_engine_security_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 10/11 offline PHP/WebCore compatibility

set "CURRENT_STAGE=11/11 clean worktree"
echo [AORebirth Gate] START %CURRENT_STAGE%
set "DIRTY=0"
for /f "delims=" %%G in ('git status --porcelain --untracked-files=all') do set "DIRTY=1"
if not "%DIRTY%"=="0" (
    echo [AORebirth Gate] FAIL 13/13 clean worktree - tracked or untracked output remains.
    goto :fail
)
echo [AORebirth Gate] PASS 11/11 clean worktree

echo [AORebirth Gate] PASS - all 11 mandatory stages completed.
popd >nul
exit /b 0

:stage_fail
echo [AORebirth Gate] FAIL %CURRENT_STAGE% - command returned a nonzero exit code.

:fail
popd >nul
exit /b 1
