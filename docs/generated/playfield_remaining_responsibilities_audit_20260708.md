# Playfield Remaining Responsibilities Audit

Date: 2026-07-08
Baseline: `675da7a7`

Inspected:
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldRuntimeSystems.cs`

## Summary

`Playfield` is no longer carrying the large runtime systems that have already been split out, but it still owns a mix of:

- stable public façade entry points,
- packet construction/send callbacks,
- same-playfield teleport/grid handling,
- global/cross-playfield lookup/shutdown hooks,
- and excluded gameplay domains that are intentionally still local.

Most of the high-volume code still in `Playfield` is tied to excluded domains:

- corpse systems,
- NPC/player combat runtime,
- private city initialization,
- startup materialization,
- wall collision,
- visibility packet orchestration.

That leaves one clear non-excluded extraction seam with real payoff: same-playfield teleport/grid handling.

## Remaining Direct Responsibilities

### 1. Public API entry points

| Responsibility | Examples in `Playfield.cs` | Status |
|---|---|---|
| Stable playfield façade for external callers | `Announce`, `AnnounceOthers`, `Publish`, `Send`, `FindByIdentity`, `FindInRange`, `FindCharacterInRange`, `FindNamedEntityByIdentity`, `ListAvailablePlayfields` | should remain in `Playfield` |
| Public routing into already-extracted services | `TryHandleGenericCmdUse`, `ExecuteFunction`, `SendPrivateCityPlayfieldReadyBlock`, `SendPrivateCityPreFullCharacterReadyBlock`, `SendSCFUsToClient`, `AnnouncePlayerVisibility` | should remain in `Playfield` |
| Public lifecycle entry points for excluded domains | `StartPlayerAttack`, `CancelPlayerAttack`, `RespawnPlayer`, `TryUseCorpse`, `TryLootCorpseItem`, `DespawnNpcImmediately`, `AcquireNpcAggro` | should remain in `Playfield` |
| Cross-playfield teleport entry point | `Teleport` | risky extraction candidate once local same-playfield handling is separated |

### 2. Packet send callbacks

| Responsibility | Examples in `Playfield.cs` | Status |
|---|---|---|
| Playfield-local packet construction/send wrappers | `AnnounceAppearanceUpdate`, `Send`, `Announce`, `Publish` | should remain in `Playfield` |
| Cross-playfield transfer callbacks kept local after the new extraction | `AnnouncePlayfieldTransferDespawn`, `SendPlayfieldTransferRedirect` | should remain in `Playfield` |
| Same-playfield teleport packet send | `TryCompleteGridTeleportInCurrentPlayfield` with `TeleportMessageHandler.Default.SendLocal` | safe extraction candidate if packet send stays callback-owned by `Playfield` |
| Death/respawn/combat/corpse packet helpers | `SendDeathRespawnPlayfieldReadyBlock`, `SendPlayfieldTowersAndCities`, `SendCorpseFullUpdate`, `SendCorpseInventoryUpdate`, `SendCombatStopMessage`, `SendRewardFeedback`, `SendUseActionFinished`, `SendTargetClearMessage`, `SendCombatIdleState` | risky extraction candidate; excluded or packet-shape-sensitive |

### 3. Local teleport / grid handling

| Responsibility | Examples in `Playfield.cs` | Status |
|---|---|---|
| Same-playfield grid/local teleport completion | `TryCompleteGridTeleportInCurrentPlayfield` | safe extraction candidate |
| Post-local-teleport contact priming | `PrimeStatelCollisionContacts` | safe extraction candidate |
| Post-zone grace arming | `ArmPostZoneCollisionGrace` | safe extraction candidate |
| Playfield-id construction for statel/wall routes | `TeleportToPlayfield` | safe extraction candidate |
| Runtime collision routing into services | `CheckStatelCollision`, `CheckWallCollision` | should remain in `Playfield` as façade entry points |

Notes:
- This is the cleanest remaining cohesive seam not blocked by the task exclusions.
- It is adjacent to the already-extracted cross-playfield handoff path, so the boundary is now clearer than before.

### 4. Object / dynel registration and global lookups

| Responsibility | Examples in `Playfield.cs` | Status |
|---|---|---|
| Global shutdown/disconnect logic | `DisconnectAllClients`, `DisconnectClient` | should remain in `Playfield` |
| Cross-playfield/global counts | `NumberOfDynels`, `NumberOfPlayers` | should remain in `Playfield` |
| Registry-facing lookup façade | `FindByIdentity`, `FindInRange`, `FindCharacterInRange` | should remain in `Playfield` |
| Direct object despawn façade | `Despawn` | risky extraction candidate; tied to object lifecycle callbacks and identity behavior |

### 5. Lifecycle / startup ownership

| Responsibility | Examples in `Playfield.cs` | Status |
|---|---|---|
| Constructor and playfield boot wiring | constructor, bus setup, startup registration | should remain in `Playfield` |
| Startup object materialization orchestration | constructor call into `runtimeSystems.MaterializeStartupObjects(...)` | should remain in `Playfield` |
| Heartbeat timer entry point | `HeartBeatTimer` | should remain in `Playfield` |

Notes:
- These areas are already constrained by existing task exclusions around startup materialization and heartbeat callbacks.

### 6. Excluded direct domains still intentionally local

| Domain | Examples in `Playfield.cs` | Status |
|---|---|---|
| Corpse lifecycle / loot / credits | corpse dictionaries and helper cluster around `ScheduleCorpseSpawn`, `ProcessPendingCorpseSpawns`, `AwardCorpseCredits`, loot roll helpers | should remain in `Playfield` for this task scope |
| Player/NPC combat algorithms | `DoCombatTick`, damage/range/weapon helpers, NPC movement-to-combat helpers | should remain in `Playfield` for this task scope |
| Private-city init helpers | org/private-city resolver helpers and ready-block packet helpers | should remain in `Playfield` for this task scope |

## Recommended Next Single Extraction

### Extract next: same-playfield teleport/grid handling

Recommended boundary:

- `TryCompleteGridTeleportInCurrentPlayfield`
- `PrimeStatelCollisionContacts`
- `ArmPostZoneCollisionGrace`
- `TeleportToPlayfield`

Recommended shape:

- a focused runtime service such as `PlayfieldLocalTeleportRuntimeService` or `PlayfieldGridTeleportRuntimeService`
- `Playfield` keeps packet construction/send callbacks and playfield-id construction where needed
- runtime service owns the local teleport orchestration order:
  - send-local teleport callback
  - coordinate/heading mutation callback
  - post-teleport contact priming callback
  - post-zone grace sequencing

Why this next:

1. It is the clearest remaining cohesive responsibility not blocked by the current exclusions.
2. It directly follows the just-completed cross-playfield transfer extraction.
3. It removes real branching/orchestration from `Teleport` instead of adding wrapper churn elsewhere.
4. Existing zoning/statel guardrails already cover the surrounding packet/order constraints.

## Recommendation Against Next

Do not extract next:

- corpse/death packet helpers,
- combat packet helpers,
- shutdown/disconnect logic,
- startup materialization,
- global count/lookups.

Those are either explicitly excluded, packet-shape-sensitive, or too coupled to world/state ownership for a clean next slice.
