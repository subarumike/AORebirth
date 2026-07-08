# Playfield Remaining Responsibilities Audit

Date: 2026-07-08
Baseline: `675da7a7`
Files inspected:
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldRuntimeSystems.cs`

## Summary

`PlayfieldRuntimeSystems` now owns most extracted runtime domains. `Playfield` still directly owns a smaller set of high-coupling surfaces:
- public facade entry points
- packet construction/send callbacks
- local and cross-playfield teleport handoff logic
- some object/dynel facade methods
- startup/heartbeat/disposal orchestration
- remaining packet-heavy combat, corpse, and respawn internals that were intentionally left local

The next extraction should not be another thin wrapper pass. The best remaining cohesive slice is the playfield transfer/teleport orchestration chain behind `Teleport(...)`.

## 1. Public API Entry Points

### Should remain in Playfield
- `Announce(...)`, `Publish(...)`, `Send(...)`
- `FindByIdentity(...)`, `FindInRange(...)`, `FindCharacterInRange(...)`, `FindNamedEntityByIdentity(...)`
- `NumberOfDynels()`, `NumberOfPlayers()`
- `ListAvailablePlayfields(...)`
- `IsInstancedPlayfield()`

Reason:
- These are outward-facing facade methods over bus/session/playfield state.
- They are appropriate as `Playfield` surface area even when implementation delegates inward.

### Safe extraction candidates
- none worth extracting by themselves

### Risky extraction candidates
- `TryUseCorpse(...)`, `TryUseDeadNpcCorpse(...)`, `TryLootCorpseItem(...)`

Reason:
- These are public entry points but already delegate into extracted corpse services. Moving the public method ownership out of `Playfield` would add churn without reducing real local complexity.

## 2. Packet Send Callbacks

### Should remain in Playfield
- packet construction/send helpers passed into services:
  - `SendPlayfieldTransferRedirect(...)`
  - `SendDeathRespawnGameTime(...)`
  - `SendDeathRespawnPlayfieldReadyBlock(...)`
  - `SendPlayfieldTowersAndCities(...)`
  - `SendEmptyPlayfieldTowersAndCities(...)`
  - `SendRewardFeedback(...)`
  - `SendUseActionFinished(...)`
  - `SendTargetClearMessage(...)`
  - `SendCombatIdleState(...)`
  - `SendStatChangedMessage(...)`

Reason:
- These methods are packet-shape owners or packet-order-adjacent callbacks.
- They are exactly the kind of code that should stay near packet construction until there is a larger packet-construction boundary, not another orchestration service.

### Safe extraction candidates
- none recommended as a standalone slice

### Risky extraction candidates
- corpse/death visual send helpers:
  - `SendCorpseFullUpdate(...)`
  - `SendCorpseInventoryUpdate(...)`
  - `SendPlayerCorpseFullUpdate(...)`
  - `SendDeathRespawnAction(...)`
  - `SendNpcDeathAnimation(...)`
  - `SendPlayerDeathAnimation(...)`

Reason:
- These are packet-heavy and still coupled to combat/death/corpse state.
- They should move only as part of a larger packet-construction boundary, not piecemeal.

## 3. Local Teleport / Grid / Playfield Transfer Handling

### Remaining direct ownership
- `Teleport(...)`
- `ClearPlayfieldTransferContactState(...)`
- `DisableTimersForPlayfieldTransfer(...)`
- `AnnouncePlayfieldTransferDespawn(...)`
- `ApplyPlayfieldTransferState(...)`
- `CapturePlayfieldTransferClient(...)`
- `ResolveOrCreatePlayfieldTransferDestination(...)`
- `CompletePlayfieldTransferDispose(...)`
- `TryCompleteGridTeleportInCurrentPlayfield(...)`
- `TeleportToPlayfield(...)`

### Should remain in Playfield
- packet emission details in `SendPlayfieldTransferRedirect(...)`
- local grid teleport packet send path inside `TryCompleteGridTeleportInCurrentPlayfield(...)`

Reason:
- both remain packet-sensitive and session-sensitive

### Safe extraction candidates
- the orchestration chain around non-local transfer handoff:
  - transfer-begin sequencing
  - destination resolve/create handoff
  - despawn/disable/apply/complete callback ordering

Reason:
- `PlayfieldRuntimeSystems` already has `PlayfieldTransferRuntimeService`.
- The runtime facade already exposes:
  - `RunPlayfieldTransferBeginSequence(...)`
  - `PreparePlayfieldTransfer(...)`
  - `CompletePlayfieldTransfer(...)`
- The missing reduction is the top-level `Playfield.Teleport(...)` decision tree and helper chain still being locally owned.

### Risky extraction candidates
- private-city special routing intertwined with teleport helpers:
  - `ResolveCapturedMontroyalPrivateCityInstance(...)`
  - `ResolveOrganizationCityId(...)`
  - `ResolveOrganizationName(...)`

Reason:
- tied to special-case routing and prior private-city parity work
- should not be the first transfer extraction

## 4. Object / Dynel Registration And Removal Facade

### Should remain in Playfield
- `Despawn(...)`
- `DisconnectClient(...)`
- dynel lookup/count facade methods

Reason:
- these are playfield-facing lifecycle entry points over runtime systems and pool state

### Safe extraction candidates
- none with meaningful payoff

### Risky extraction candidates
- `DynelDropPosition(...)`

Reason:
- too small to justify another service; low payoff

## 5. Lifecycle / Startup

### Should remain in Playfield
- constructor startup wiring
- bus subscriptions
- heartbeat timer ownership
- `HeartBeatTimer(...)`
- `Dispose(...)`
- `ArmPostZoneCollisionGrace(...)`
- `IsPrivateCityPlayfieldCandidate(...)`

Reason:
- these are playfield lifetime concerns, not subsystem runtime seams

### Safe extraction candidates
- none recommended

### Risky extraction candidates
- heartbeat callback breakup into more wrapper services

Reason:
- high churn, low ownership payoff

## 6. Other Remaining Direct Responsibilities

### Should remain in Playfield for now
- combat math / weapon resolution / damage packet construction
- corpse loot table roll and item materialization internals
- respawn destination resolution and death/respawn packet construction

### Safe extraction candidates
- none in this audit slice

### Risky extraction candidates
- combat math block
- corpse loot generation block
- death/respawn packet chain

Reason:
- each is still packet-heavy, algorithm-heavy, or state-heavy
- none is a clean next extraction after the current runtime-service decomposition

## Recommended Next Single Extraction

Extract the **non-local playfield transfer orchestration chain** behind `Playfield.Teleport(...)` into the existing `PlayfieldTransferRuntimeService`.

### Why this one
- It is the largest remaining cohesive runtime responsibility still directly owned by `Playfield`.
- It already has an existing service boundary in `PlayfieldRuntimeSystems`.
- It reduces real `Playfield` ownership instead of adding another one-method wrapper.
- It avoids packet-construction movement if scoped correctly.

### Recommended scope
- Move orchestration only:
  - transfer-begin callback sequencing
  - destination resolve/create routing
  - transfer state mutation ordering
  - despawn/dispose/handoff callback ordering
- Keep in `Playfield`:
  - redirect packet construction/send
  - local current-grid teleport packet path
  - private-city special-case packet details

### Why not choose packet callbacks next
- Most remaining packet callbacks are construction owners, not orchestration seams.
- Moving them now would create wrapper churn without reducing risk or complexity.
