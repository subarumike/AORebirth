@echo off
setlocal EnableExtensions

pushd "%~dp0.." >nul
if errorlevel 1 (
    echo [AORebirth Gate] FAIL - cannot enter repository root.
    exit /b 1
)

call :RequireFile tools\scan_secrets.cmd
if errorlevel 1 goto :fail
call :RequireFile tools\generate_capture_backed_npc_combat_inventory.cmd
if errorlevel 1 goto :fail
call :RequireFile tools\run_aotomation_messaging_tests.cmd
if errorlevel 1 goto :fail
call :RequireFile tools\run_subway_acceptance_tests.cmd
if errorlevel 1 goto :fail
call :RequireFile tools\run_temple_acceptance_tests.cmd
if errorlevel 1 goto :fail
call :RequireFile tools\generate_mission_level_graph.cmd
if errorlevel 1 goto :fail
call :RequireFile tools\build_aorebirth_debug.cmd
if errorlevel 1 goto :fail
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

set "CURRENT_STAGE=1/11 secret scan"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\scan_secrets.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 1/11 secret scan

set "CURRENT_STAGE=2/11 generated artifact reproducibility"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\generate_capture_backed_npc_combat_inventory.cmd --check
if errorlevel 1 goto :stage_fail
python tools-temp\AOSharpCaptureAnalyzer\generate_capture_backed_npc_active_coverage.py --check
if errorlevel 1 goto :stage_fail
python tools-temp\AOSharpCaptureAnalyzer\analyze_enemy_combat_setup_formula.py --check
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 2/11 generated artifact reproducibility

set "CURRENT_STAGE=3/11 complete AOtomation suite"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_aotomation_messaging_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 3/11 complete AOtomation suite

set "CURRENT_STAGE=4/11 Arete acceptance 60/60"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~AreteAcceptanceMatrixTests"
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 4/11 Arete acceptance 60/60

set "CURRENT_STAGE=5/11 Arete combat catalog active coverage and loot"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatProfileCatalogTests"
if errorlevel 1 goto :stage_fail
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatActiveCoverageTests"
if errorlevel 1 goto :stage_fail
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests"
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 5/11 Arete combat catalog active coverage and loot

set "CURRENT_STAGE=6/11 Subway acceptance"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_subway_acceptance_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 6/11 Subway acceptance

set "CURRENT_STAGE=7/11 Temple acceptance"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\run_temple_acceptance_tests.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 7/11 Temple acceptance

set "CURRENT_STAGE=8/11 mission graph and generated mission reproducibility"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\generate_mission_level_graph.cmd --check
if errorlevel 1 goto :stage_fail
call tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~Mission"
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 8/11 mission graph and generated mission reproducibility

set "CURRENT_STAGE=9/11 Git LFS integrity"
echo [AORebirth Gate] START %CURRENT_STAGE%
git lfs fsck
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 9/11 Git LFS integrity

set "CURRENT_STAGE=10/11 debug server build"
echo [AORebirth Gate] START %CURRENT_STAGE%
call tools\build_aorebirth_debug.cmd
if errorlevel 1 goto :stage_fail
echo [AORebirth Gate] PASS 10/11 debug server build

set "CURRENT_STAGE=11/11 clean worktree"
echo [AORebirth Gate] START %CURRENT_STAGE%
set "DIRTY=0"
for /f "delims=" %%G in ('git status --porcelain --untracked-files=all') do set "DIRTY=1"
if not "%DIRTY%"=="0" (
    echo [AORebirth Gate] FAIL 11/11 clean worktree - tracked or untracked output remains.
    goto :fail
)
echo [AORebirth Gate] PASS 11/11 clean worktree

echo [AORebirth Gate] PASS - all 11 mandatory stages completed.
popd >nul
exit /b 0

:RequireFile
if not exist "%~1" (
    echo [AORebirth Gate] FAIL - required file is missing: %~1
    exit /b 1
)
exit /b 0

:stage_fail
echo [AORebirth Gate] FAIL %CURRENT_STAGE% - command returned a nonzero exit code.

:fail
popd >nul
exit /b 1
