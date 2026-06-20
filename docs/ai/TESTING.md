# Testing

Generated: 2026-06-02

## Build Verification

Use this command from repo root:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'AORebirth\AORebirth.sln' /t:Build /p:Configuration=Debug /m
```

If engines are running and lock binaries, stop them first:

```powershell
Get-Process ChatEngine,LoginEngine,ZoneEngine,WebEngine,MSBuild -ErrorAction SilentlyContinue | Stop-Process -Force
```

## Automated Smoke Tests

The `CellAOCombatSmokeTests` directory name is a legacy tool folder name retained in-place; it is not current repository branding or a reason to rename AO Rebirth.

Primary smoke/source assertion script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\AORebirth\tools-temp\CellAOCombatSmokeTests\Run-CombatSmokeTests.ps1'
```

Skip build when the solution was already built:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\AORebirth\tools-temp\CellAOCombatSmokeTests\Run-CombatSmokeTests.ps1' -SkipBuild
```

Enemy movement replay:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\AORebirth\tools-temp\enemy-movement-replay\Test-EnemyMovementReplay.ps1'
```

Live data collector self-check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mike\Documents\AORebirth\tools-temp\live-data-collector\Test-LiveDataCollector.ps1'
```

## Manual Testing

Mike performs live client playtests. Codex should:

- build the server,
- start/stop engines when asked,
- provide exact commands to run in game,
- watch logs/captures if needed,
- avoid asking Mike to tab rapidly or perform unclear sequences.

Manual test areas:

- login and playfield entry,
- sit/stand,
- equipment and visual mesh,
- attack with weapon and dual wield,
- death and respawn,
- corpse use and loot,
- player trade,
- NPC spawn and combat,
- logout close-box behavior.

## Regression Testing

Do not regress:

- `FullCharacter` version 26,
- live-style login movement/social state,
- no fake action bootstrap packets,
- weapon visual meshes after equip/relog,
- equipped item stats and persistence,
- death/respawn visibility fix,
- `AttackInfo`-only normal auto-attacks,
- corpse despawn after looting,
- unique-item loot checks,
- current database safety rules.

## Performance Testing

Current performance testing is informal. Watch for:

- server lag while another heavy compile is running,
- high-volume debug logs,
- repeated movement packet spam,
- packet capture overhead,
- client rendering or WebView errors unrelated to server logic.

TODO: Requires human clarification for formal performance targets.

## Acceptance Criteria

For packet/gameplay repairs:

- Evidence source is identified.
- Change is scoped to the affected path.
- Build succeeds.
- Smoke/source assertion is added or updated when practical.
- Mike completes a focused playtest when client behavior matters.
- Final report states what was verified and what remains unknown.
