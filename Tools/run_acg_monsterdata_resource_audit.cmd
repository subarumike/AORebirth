@echo off
setlocal
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\export_acg_monsterdata_resource_sources.ps1 || exit /b 1
python -m unittest Tools.tests.test_acg_monsterdata_resource_audit || exit /b 1
python -m unittest Tools.tests.test_enemy_archetype_census || exit /b 1
python -m unittest Tools.tests.test_npc_observation_harvester || exit /b 1
python -m unittest Tools.tests.test_npc_placement_identity_resolver || exit /b 1
python Tools\acg_monsterdata_resource_audit.py %* || exit /b 1
echo TESTS=PASS
exit /b 0
