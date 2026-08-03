@echo off
setlocal EnableExtensions

pushd "%~dp0.." >nul
if errorlevel 1 (
    echo [AORebirth Gate] FAIL - cannot enter repository root.
    exit /b 1
)

for %%F in (
    "tools\scan_secrets.cmd"
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
where python >nul 2>nul
if errorlevel 1 (
    echo [AORebirth Gate] FAIL - python is unavailable.
    goto :fail
)
git lfs version >nul 2>nul
if errorlevel 1 (
    echo [AORebirth Gate] FAIL - Git LFS is unavailable.
    goto :fail
)

set "CURRENT_STAGE=1/13 secret scan"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\scan_secrets.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 1/13 secret scan

set "CURRENT_STAGE=2/13 engine management safety contracts"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_engine_management_tests.cmd --skip-web-engine-security
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 2/13 engine management safety contracts

set "CURRENT_STAGE=3/13 generated artifact reproducibility"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_generated_combat_concurrency_tests.cmd
if errorlevel 1 goto :stage_fail
call tools\generate_capture_backed_npc_combat_inventory.cmd --check
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 3/13 generated artifact reproducibility

set "CURRENT_STAGE=4/13 complete AOtomation suite"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_aotomation_messaging_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 4/13 complete AOtomation suite

set "CURRENT_STAGE=5/13 Arete acceptance 60/60"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~AreteAcceptanceMatrixTests"
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 5/13 Arete acceptance 60/60

set "CURRENT_STAGE=6/13 Arete combat catalog active coverage and loot"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatProfileCatalogTests"
if errorlevel 1 goto :stage_fail
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatActiveCoverageTests"
if errorlevel 1 goto :stage_fail
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests"
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 6/13 Arete combat catalog active coverage and loot

set "CURRENT_STAGE=7/13 Subway acceptance"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_subway_acceptance_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 7/13 Subway acceptance

set "CURRENT_STAGE=8/13 Temple acceptance"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_temple_acceptance_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 8/13 Temple acceptance

set "CURRENT_STAGE=9/13 mission graph and generated mission reproducibility"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\generate_mission_level_graph.cmd --check
if errorlevel 1 goto :stage_fail
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~Mission"
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 9/13 mission graph and generated mission reproducibility

set "CURRENT_STAGE=10/13 Git LFS integrity"
echo [AORebirth Gate] START %CURRENT_STAGE%
git lfs fsck
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 10/13 Git LFS integrity

set "CURRENT_STAGE=11/13 debug server build"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\build_aorebirth_debug.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 11/13 debug server build

set "CURRENT_STAGE=12/13 offline PHP/WebCore compatibility"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_web_engine_security_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 12/13 offline PHP/WebCore compatibility

set "CURRENT_STAGE=13/13 clean worktree"
echo [AORebirth Gate] START %CURRENT_STAGE%
set "DIRTY=0"
for /f "delims=" %%G in ('git status --porcelain --untracked-files=all') do set "DIRTY=1"
if not "%DIRTY%"=="0" (
    echo [AORebirth Gate] FAIL 13/13 clean worktree - tracked or untracked output remains.
    goto :fail
)
echo [AORebirth Gate] PASS 13/13 clean worktree

echo [AORebirth Gate] PASS - all 13 mandatory stages completed.
popd >nul
exit /b 0

:stage_fail
echo [AORebirth Gate] FAIL %CURRENT_STAGE% - command returned a nonzero exit code.

:fail
popd >nul
exit /b 1
