# Arete Malfunctioning Cleaning Robot Spawn Result

Generated: 2026-06-17

## Summary

Added a minimum evidence-backed SQL/data patch for five `Malfunctioning Cleaning Robot` mobspawns in playfield `6553` Arete Landing so the next Rex B18C smoke test can use runtime NPC deaths.

Follow-up load-screen smoke exposed that captured SCFU-visible stats alone were not enough for AO Rebirth's existing heartbeat and SimpleCharFullUpdate paths. The SQL patch now keeps the same five captured spawn rows and captured HP/level/monster data, plus a separated runtime actor-baseline scaffold block required to keep derived stat calculations and packet scalar fields inside safe bounds.

This was spawn rows only. No Cargo Box, B18D, B18E, quest completion, rewards, inventory, XP/credits, schema changes, validation tooling, packet replay, or live quest smoke was added.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/generated/rex_b18c_live_objective_progress_result.md`
- `tools-temp/arete-analysis/enemy_observations.json`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/enemy-state.csv`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/enemy-state.json`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobspawns.sql`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/mobspawns_stats.sql`
- `AORebirth/Libraries/Source/AORebirth.Database/Entities/DBMobSpawn.cs`
- `AORebirth/Libraries/Source/AORebirth.Database/Entities/DBMobSpawnStat.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/NPCHandler/NonPlayerCharacterHandler.cs`
- `AORebirth/Config/Config.xml`

## Files Changed

- `tools-temp/sql-staging/arete_malfunctioning_cleaning_robot_mobspawns.sql`
- `docs/generated/arete_malfunctioning_cleaning_robot_spawn_result.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`

## Schema And Loader Mapping

Confirmed `mobspawns` columns:

- `Id`
- `Playfield`
- `X`, `Y`, `Z`
- `HeadingX`, `HeadingY`, `HeadingZ`, `HeadingW`
- `Name`
- `Textures0` through `Textures4`
- `Waypoints`
- `Weaponpairs`
- `RunningNanos`
- `MobMeshs`
- `AdditionalMeshs`
- `KnuBotScriptName`

Confirmed `mobspawns_stats` columns:

- `Id`
- `Playfield`
- `Stat`
- `Value`

Runtime mapping:

- `Playfield.LoadMobSpawns(...)` loads `mobspawns` by playfield.
- It then loads matching `mobspawns_stats` by `(Id, Playfield)`.
- It instantiates rows through `NonPlayerCharacterHandler.InstantiateMobSpawn(...)`.
- The runtime mobspawn path does not require a `mobtemplate` row for `monsterData=297023`.

Local DB verification still shows `0` `mobtemplate` rows for `MonsterData=297023`.

## Captured Robot Rows Used

The patch uses five distinct captured robot identities from `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454`.

| Hex identity | Decimal id | Evidence | Position | Heading Y/W | Captured metadata |
| --- | ---: | --- | --- | --- | --- |
| `78D3ACB7` | `2027138231` | `enemy-state.csv:69`, `enemy-state.json:2172`, `events.log:229`, death `events.log:3813` | `(3608.66138, 51.745, 795.9552)` | `0.7361878 / 0.6767774` | level `1`, HP `12/12`, `monsterData=297023` |
| `78D3ACC5` | `2027138245` | `enemy-state.csv:779`, `enemy-state.json:2358`, `events.log:2650`, death `events.log:3413` | `(3598.61523, 51.745, 774.0247)` | `0.7383381 / 0.6744308` | level `1`, HP `12/12`, `monsterData=297023` |
| `78D3ACC6` | `2027138246` | `enemy-state.csv:805`, `events.log:2720`, death `events.log:3247` | `(3606.319, 51.745, 801.3757)` | `0.734597 / 0.6785036` | level `1`, HP `12/12`, `monsterData=297023` |
| `78D3ACC9` | `2027138249` | `events.log:3392`, death `events.log:3565` | `(3617.60181, 51.745, 783.974731)` | `0.7344052 / 0.6787114` | level `1`, HP `12/12`, `monsterData=297023` |
| `78D3ACD3` | `2027138259` | `events.log:5106`, death `events.log:5374` | `(3607.9126, 51.745, 796.260254)` | `0.7348164 / 0.678266` | level `1`, HP `12/12`, `monsterData=297023` |

The selected set intentionally uses only five named robots with spawn/full-update and later death evidence.

## SQL Patch

Patch path:

```text
tools-temp/sql-staging/arete_malfunctioning_cleaning_robot_mobspawns.sql
```

Patch behavior:

- Deletes/reinserts only the five selected robot IDs in playfield `6553`.
- Inserts exactly five `mobspawns` rows named `Malfunctioning Cleaning Robot`.
- Inserts 11 captured `mobspawns_stats` rows per robot:
  - `Stat 0 = 268964353`
  - `Stat 1 = 12`
  - `Stat 27 = 12`
  - `Stat 54 = 1`
  - `Stat 64 = 0`
  - `Stat 156 = 5`
  - `Stat 359 = 297023`
  - `Stat 360 = 200`
  - `Stat 389 = 0`
  - `Stat 660 = 0`
  - `Stat 673 = 31`
- Inserts 16 runtime actor-baseline scaffold rows per robot:
  - `Stat 4 = 1`
  - `Stat 18 = 0`
  - `Stat 21 = 0`
  - `Stat 33 = 0`
  - `Stat 37 = 1`
  - `Stat 47 = 1`
  - `Stat 59 = 0`
  - `Stat 60 = 15`
  - `Stat 89 = 1`
  - `Stat 132 = 5`
  - `Stat 152 = 5`
  - `Stat 173 = 3`
  - `Stat 214 = 1`
  - `Stat 368 = 15`
  - `Stat 455 = 0`
  - `Stat 466 = 15`
- Uses empty blob values for waypoint, weapon, nano, and mesh blob columns so the existing loader does not hit null collection deserialization.
- Adds no scripts, waypoints, rewards, dialogue, quest state, inventory, Cargo Box data, or other Arete actors.

The scaffold rows are not treated as captured robot gameplay semantics. They are runtime safety rows based on AO Rebirth defaults, the existing mob creation baseline, and values required by `Playfield.HeartBeatTimer` and `SimpleCharFullUpdate`.

## Local Apply

The task resumed with an earlier broader draft already applied locally. Before the captured-only reapply, the DB had:

| Check | Result |
| --- | ---: |
| total `mobspawns` rows in `6553` | `6` |
| `Malfunctioning Cleaning Robot` rows in `6553` | `5` |
| unrelated rows in `6553` | `0` |
| stats rows per selected robot | `24` |

The captured-only patch was then sourced into `cellao_codex_clean` using a temporary MySQL defaults file:

```powershell
mysql --defaults-extra-file=<temp-auth-file> --execute "SOURCE tools-temp/sql-staging/arete_malfunctioning_cleaning_robot_mobspawns.sql"
```

The temporary auth file was removed after execution.

## DB Verification

After heartbeat-safe reapply:

| Check | Result |
| --- | ---: |
| total `mobspawns` rows in `6553` | `6` |
| `Malfunctioning Cleaning Robot` rows in `6553` | `5` |
| unrelated rows in `6553` | `0` |
| stats rows per selected robot | `27` |
| each selected robot has level `1` | yes |
| each selected robot has life/current health `12/12` | yes |
| each selected robot has `monsterData=297023` | yes |
| `mobtemplate` rows with `MonsterData=297023` | `0` |

Current local playfield `6553` `mobspawns` rows are:

| Id | Name | X | Y | Z |
| ---: | --- | ---: | ---: | ---: |
| `2016273768` | `Rex Larsson` | `3624.59912` | `51.745` | `787.74652` |
| `2027138231` | `Malfunctioning Cleaning Robot` | `3608.66138` | `51.745` | `795.9552` |
| `2027138245` | `Malfunctioning Cleaning Robot` | `3598.61523` | `51.745` | `774.02472` |
| `2027138246` | `Malfunctioning Cleaning Robot` | `3606.31909` | `51.745` | `801.37567` |
| `2027138249` | `Malfunctioning Cleaning Robot` | `3617.60181` | `51.745` | `783.97473` |
| `2027138259` | `Malfunctioning Cleaning Robot` | `3607.9126` | `51.745` | `796.26025` |

No unrelated `6553` rows were added.

## Validation

Completed:

- Local DB before/after count queries.
- Local DB robot row verification queries.
- Local DB unrelated `6553` row query.
- Local DB per-robot captured stat verification.
- Local DB per-robot runtime scaffold stat verification.
- Engine stop/start after the heartbeat-safe reapply.

Build was not run because no code or project files changed in this spawn-only phase.

Pending:

- `git diff --check`.
- Manual quest smoke in the next prompt.

## Rollback

To remove only these five local robot spawn rows:

```sql
START TRANSACTION;
DELETE FROM mobspawns_stats
WHERE Playfield = 6553
  AND Id IN (2027138231, 2027138245, 2027138246, 2027138249, 2027138259);
DELETE FROM mobspawns
WHERE Playfield = 6553
  AND Id IN (2027138231, 2027138245, 2027138246, 2027138249, 2027138259);
COMMIT;
```

Then restart ZoneEngine so the playfield reloads without the rows.

## Remaining Blockers

- The next prompt still needs the gated live smoke test against these runtime spawn rows.
- The robots are stationary because no evidence-backed waypoint/pathing rows were added.
- Quest completion, rewards, progress refresh packets, `Quest Delete`, action `59`, B18D, B18E, and Cargo Box behavior remain intentionally out of scope.
