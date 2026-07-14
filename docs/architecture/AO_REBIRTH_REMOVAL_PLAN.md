# AORebirth Removal Plan

| Candidate | Disposition | Replacement owner | Dependency | Risk and required test |
| --- | --- | --- | --- | --- |
| `Playfield.RollCapturedCleaningRobotLootItems` | REMOVED | global loot service | captured-outcome parity adapter | Deterministic parity covered |
| `Playfield.DebugLootTable` path | REMOVED | explicit debug assignment | loot resolver | Isolated from ordinary production selection |
| Captured ordinary runtime loot conversion | REPLACED | global registry adapter | normalized definitions | Thief and Filth Flea use shared generation |
| `Playfield.GetDatabaseLootTable` static cache | REMOVED | indexed registry DB adapter | DB adapter | Missing/invalid data fails closed |
| `CapturedAreteRobotSpawnOrchestrator` | CONSOLIDATE | generic spawn controller | population adapter and packet fixtures | High; Arete spawn/movement/combat/corpse parity |
| `PlayfieldDbMobSpawnRuntimeService` direct materialization policy | CONSOLIDATE | generic spawn controller | legacy DB definition adapter | High; startup population parity |
| `OrdinaryEnemyRuntimeService.ScheduleRespawnAfterDespawn` and `pendingRespawns` | REMOVED | `WorldPopulationController` + `WorldRespawnScheduler` | ordinary adapter | Deterministic scheduler/lifecycle guards |
| `OrdinaryEnemyRuntimeService.SpawnForPlayfield` | REMOVED | `WorldPopulationController.ActivatePlayfield` | ordinary catalog adapter | Population/quarantine parity guards |
| `TradeMessageHandler.cs.orig` | REMOVE | active handler/history | confirm no tooling reference | Low; build and source-reference guard |
| `*.sql.obsolete` schema files | DEPRECATE then archive/remove | migrations/reference history | schema inventory approval | Medium; explicit Mike approval required |
| PowerShell workflow scripts marked deprecated | DEPRECATE | approved cmd wrappers | wrapper coverage | Low; workflow docs/tests |
| generated C# population arrays | REPLACE gradually | versioned data loader | schema/loader validation | High; deterministic output and startup cost tests |

Do not remove any candidate until its replacement is active, parity tests pass, rollout succeeds where client behavior matters, and Git history preserves provenance. Database schema removals require explicit approval.
