# Testing

Generated: 2026-06-02

## Build Verification

Use this command from repo root:

```cmd
cmd /d /c tools\build_aorebirth_debug.cmd
```

Never use raw AORebirth `MSBuild /m`, MSBuild node reuse, PowerShell, or `.ps1` wrappers for Codex build validation. If engines are running and lock binaries, stop them through an approved `cmd.exe` or Git Bash workflow only.

The build wrapper checks required package folders before MSBuild and runs explicit MSBuild solution restore only before the build when required packages are missing. Legacy project-level `RestorePackages` imports from `.nuget\NuGet.targets` have been removed and must not be reintroduced.

## Automated Smoke Tests

Run test scripts only through approved `cmd.exe` or Git Bash workflows. PowerShell and `.ps1` validation wrappers are deprecated for Codex use. If a command fails with permission or access denied, stop and report the exact command, working directory, target path, and full error before retrying with any elevated Codex permission.

The `CellAOCombatSmokeTests` directory name is a legacy tool folder name retained in-place; it is not current repository branding or a reason to rename AO Rebirth.

Existing PowerShell smoke, replay, and live-data scripts must not be run by Codex until they have `cmd.exe` or Git Bash wrappers.

## Manual Testing

Mike performs live client playtests. Codex should:

- build the server,
- start/stop engines only through approved `cmd.exe` or Git Bash workflows when asked,
- avoid `.ps1` engine status wrappers until replaced,
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
