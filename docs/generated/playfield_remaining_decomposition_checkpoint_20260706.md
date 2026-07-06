# Playfield Remaining Decomposition Checkpoint - 2026-07-06

## Scope

This checkpoint audits remaining `Playfield` responsibilities after the
runtime-service consolidation. No runtime code was moved in this slice.

## Current Runtime Boundaries

`PlayfieldRuntimeSystems` is the facade between `Playfield` and specialized
runtime services:

- `PlayfieldContentCoordinator` and `PlayfieldContentDataProvider` own content
  module registration and raw content data selection/filtering.
- `PlayfieldDynelRegistry` owns playfield-local dynel lookup and typed views.
- `PacketSequencingCoordinator` owns session-ready, visibility-pair,
  private-city, and zoning-entry sequencing decisions without owning packet
  construction.
- `PrivateCityReadyInitCoordinator` owns private-city ready/init orchestration.
- `InventoryContainerRuntimeService` owns inventory/container orchestration.
- `NPCRuntimeService` owns NPC spawn, activation, patrol, combat, death,
  corpse/despawn timing, and NPC runtime registry orchestration.
- `PlayerCombatRuntimeService` owns player attack start, cancel/stop, tick,
  invalid-target cleanup, and death-side combat cleanup orchestration.
- `PlayfieldTimedLifecycleRuntimeService` owns heartbeat ordering.
- `PlayfieldLifecycleRuntimeService` owns player respawn and playfield-transfer
  lifecycle sequencing.
- `PlayfieldInteractionRuntimeService` owns GenericCmd use dispatch ordering.
- `PlayfieldRewardRuntimeService` owns NPC death reward hook ordering.
- `PlayfieldObjectLifecycleRuntimeService` owns safe object removal, corpse
  spawn callback order, and corpse despawn cleanup order.

## Remaining Playfield-Owned Responsibilities

`Playfield` still intentionally owns:

- packet construction and send/announce call sites
- playfield object construction from DB/static content data
- transport, teleport, redirect, and cross-playfield handoff mechanics
- player visibility SCFU/CharInPlay packet construction
- statel collision checks and collision contact state
- captured Montroyal private-city entry/exit rules
- combat damage, range, timing, source selection, and combat packet payloads
- player death/respawn stat and packet construction
- corpse state storage, corpse loot/credit rules, and corpse container packets
- loot table selection and item materialization
- global/cross-playfield `Pool` calls that are explicitly guarded

## Prioritized Next Decomposition Candidates

1. Corpse loot/access runtime boundary
   - Payoff: high.
   - Risk: medium-high.
   - Candidate scope: `TryUseCorpse`, `TryLootCorpseItem`, corpse access action,
     corpse inventory update/credit award scheduling, and corpse lifetime
     extension orchestration.
   - Keep outside: packet serialization, item creation, credit math, inventory
     mutation algorithms, and corpse storage until guardrails are stronger.
   - Why next: this is the largest remaining cohesive non-combat domain in
     `Playfield`, and inventory/object lifecycle boundaries already exist.

2. Statel collision and private-city transition boundary
   - Payoff: high.
   - Risk: medium.
   - Candidate scope: `CheckStatelCollision`, statel contact tracking,
     post-zone collision grace checks, and captured Montroyal private-city
     entry/exit orchestration.
   - Keep outside: teleport packet construction, destination playfield handoff,
     and private-city ready/init packet construction.
   - Why next: collision, statel events, and private-city transition rules are
     currently interleaved in `Playfield` despite being a distinct runtime
     interaction domain.

3. Player death/respawn packet-state boundary
   - Payoff: medium-high.
   - Risk: medium-high.
   - Candidate scope: `KillPlayerTarget`, player death/respawn stat updates,
     death social/status/action packet ordering, and respawn location
     resolution callbacks.
   - Keep outside: player death algorithms, transport handoff, packet
     serialization, and existing lifecycle coordinator sequencing until
     packet-order guardrails are expanded.
   - Why next: player lifecycle sequencing is partially extracted, but
     state/packet construction is still clustered in `Playfield`.

4. Runtime object construction/factory boundary
   - Payoff: medium.
   - Risk: medium.
   - Candidate scope: runtime construction in `LoadStaticDynels`, `LoadVendors`,
     and `LoadMobSpawns` after data selection has already been moved to
     `PlayfieldContentDataProvider`.
   - Keep outside: DB import/loading, spawn order, packet emission, registration
     side effects, and gameplay values.
   - Why next: content data selection is already separated, leaving object
     materialization as the remaining load-time ownership seam.

5. Combat payload/algorithm boundary
   - Payoff: very high.
   - Risk: high.
   - Candidate scope: damage source selection, weapon source resolution, combat
     range/timing calculation, and combat packet payload construction.
   - Keep outside until later: any change without focused guardrails and
     capture-backed parity tests.
   - Why not first: combat is the highest behavioral-risk area and still mixes
     algorithms, packet payloads, and movement decisions.

## Packet-Heavy Areas To Keep Put For Now

- `Teleport` and cross-playfield redirection should stay in `Playfield` until a
  dedicated teleport/transfer guardrail is added beyond current sequencing
  checks.
- `SendSCFUsToClient` and `AnnouncePlayerVisibility` should stay in
  `Playfield` because they build and send visibility packets, even though
  packet pair ordering is sequenced through `PacketSequencingCoordinator`.
- combat damage announcement should stay in `Playfield` until combat payload
  fixtures exist.

## Guardrail Decision

No new guardrail was added in this slice. Existing guardrails already protect
the current extracted boundaries; the next implementation slice should add
focused guardrails for whichever candidate is selected before moving behavior.
