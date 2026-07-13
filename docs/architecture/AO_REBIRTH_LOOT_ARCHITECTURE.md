# AORebirth Global Loot Architecture

## Domain model

`LootTable` contains `LootTableKey`, `DisplayName`, `TableType`, ordered `RollGroups`, `CreditsPolicy`, `QualityPolicy`, `Evidence`, and `Confidence`.

`LootGroup` contains `GroupKey`, `RollMode` (`INDEPENDENT`, `WEIGHTED_ONE`, `WEIGHTED_MANY`, `GUARANTEED_ALL`), `RollCount`, `Guaranteed`, `Weight`, entries, and conditions.

`LootEntry` contains item template low/high identity where needed, `MinimumQuality`, `MaximumQuality`, optional fixed quality, weight/drop chance, quantity range, `UniquePerCorpse`, conditions, evidence, and confidence.

`LootAssignment` contains `AssignmentKey`, target type/key, table key, optional zone/playfield/encounter/level range, priority, and conditions.

## Runtime boundaries

1. `LootAssignmentResolver` resolves applicable tables using immutable kill context.
2. `LootGenerationService` rolls with an injected deterministic random source and returns `GeneratedLoot`.
3. `CorpseInventoryService` owns remaining items, credits, inventory handle, open/close state, and empty status.
4. `LootRightsService` decides who may inspect/transfer and when rights expire.
5. Item transfer owns unique/stack/inventory validation and persistence.
6. Corpse lifecycle owns despawn, not loot definitions.

Combat emits death context and never selects tables.

## Supported policy

The model supports family/type/boss/dyna/zone/dungeon/mission/quest/raid/event tables; level and QL ranges; guaranteed, weighted, independent, and exclusive rolls; rare/unique items; credits; no-drop/profession/faction/quest conditions; team/personal policy; modifiers; lockouts; ownership and rights.

## Inheritance and precedence

Merge from least to most specific: global defaults -> zone/dungeon/mission policy -> family -> enemy type -> dyna global -> dyna level band -> dyna family -> specific boss/camp -> spawn -> encounter/event override. A group may append, replace a named inherited group, or suppress it explicitly. Assignment resolution sorts by specificity, explicit priority, then stable key. Ambiguous equal-priority replacements fail validation.

## Evidence policy

- `GUARANTEED_PROVEN`: may create a guaranteed entry.
- `OBSERVED_AVAILABLE_LOOT`: proves availability only; represents a non-guaranteed candidate with sample metadata, not a derived probability unless the sampling protocol supports it.
- `COMMUNITY_DOCUMENTED`: import as review proposal, inactive by default.
- `INFERRED`: inactive unless separately approved.
- `UNRESOLVED`: cannot generate runtime loot.

Every definition records source id, observation count where relevant, provenance, and confidence. One observed drop never becomes guaranteed.

## Current migration

- Retain DB `mobdroptable` as a legacy input adapter.
- Migrate captured ordinary `BuildCapturedLootEntries` output into evidence-bearing tables.
- Migrate cleaning-robot outcomes into one capture-backed table preserving exact distribution semantics.
- Remove `DebugLootTable` from production after fixture parity.
- Extract `Playfield.cs:3293-3856` into the services above without changing packets.

## Corpse lifecycle contract

Create corpse identity and immutable generated loot at death; assign rights; open produces the captured inventory shape; close/reopen follows captured handle progression; transfer is atomic; credits are claimed once; empty/fully-looted cleanup uses policy; unlooted lifetime is independent. Disconnect releases access leases but not ownership. Multi-player, team/personal, restart, and unproven reopen edge behavior remain `EVIDENCE_BLOCKED` until protocol evidence exists.
