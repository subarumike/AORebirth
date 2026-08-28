@echo off
setlocal
cd /d "%~dp0.."
cmd /d /c Tools\harvest_npc_observations.cmd || exit /b 1
cmd /d /c Tools\resolve_npc_placement_identity.cmd || exit /b 1
if /I "%~1"=="--check" (
  cmd /d /c Tools\run_enemy_archetype_census.cmd --check || exit /b 1
) else (
  cmd /d /c Tools\run_enemy_archetype_census.cmd || exit /b 1
)
python -m unittest Tools.tests.test_spawn_population_reconstruction || exit /b 1
python Tools\spawn_population_reconstruction.py %* || exit /b 1
echo TESTS=PASS
exit /b 0
