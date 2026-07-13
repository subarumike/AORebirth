# AORebirth Removal Plan

| Candidate | Disposition | Replacement owner | Dependency | Risk and required test |
| --- | --- | --- | --- | --- |
| `Playfield.RollCapturedCleaningRobotLootItems` | REMOVE | global loot service | captured-outcome parity adapter | High; deterministic outcome fixture and corpse parity |
| `Playfield.DebugLootTable` path | REMOVE | versioned test content repository | loot resolver | Medium; debug/smoke fixture parity |
| `CapturedSubwayOrdinaryContentProvider.BuildCapturedLootEntries` runtime conversion | DEPRECATE | evidence importer + loot tables | normalized schema | High; all active PF127 tables compare equal |
| `Playfield.GetDatabaseLootTable` static cache | REPLACE | indexed loot repository | DB adapter | Medium; load/error/precedence tests |
| `CapturedAreteRobotSpawnOrchestrator` | CONSOLIDATE | generic spawn controller | population adapter and packet fixtures | High; Arete spawn/movement/combat/corpse parity |
| `PlayfieldDbMobSpawnRuntimeService` direct materialization policy | CONSOLIDATE | generic spawn controller | legacy DB definition adapter | High; startup population parity |
| `TradeMessageHandler.cs.orig` | REMOVE | active handler/history | confirm no tooling reference | Low; build and source-reference guard |
| `*.sql.obsolete` schema files | DEPRECATE then archive/remove | migrations/reference history | schema inventory approval | Medium; explicit Mike approval required |
| PowerShell workflow scripts marked deprecated | DEPRECATE | approved cmd wrappers | wrapper coverage | Low; workflow docs/tests |
| generated C# population arrays | REPLACE gradually | versioned data loader | schema/loader validation | High; deterministic output and startup cost tests |

Do not remove any candidate until its replacement is active, parity tests pass, rollout succeeds where client behavior matters, and Git history preserves provenance. Database schema removals require explicit approval.
