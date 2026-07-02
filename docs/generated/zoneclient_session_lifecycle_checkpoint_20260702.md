# ZoneClient Session Lifecycle Checkpoint

Date: 2026-07-02

## Scope

This checkpoint records the current architecture boundary work for Playfield runtime systems, content data providers, and ZoneClient session lifecycle coordination. It documents architecture progress only. It does not document gameplay behavior changes.

## PlayfieldDynelRegistry Progress

- `PlayfieldDynelRegistry` sits behind `PlayfieldRuntimeSystems`.
- Safe typed lookup surfaces have moved behind the registry:
  - identity lookup
  - typed identity lookup
  - dynel range lookup
  - character/player views used by visibility and same-playfield lifecycle paths
- Remaining direct `Pool` usage in `Playfield` is guarded as named global or cross-playfield exceptions:
  - `DisconnectAllClients`
  - `NumberOfDynels`
  - `NumberOfPlayers`
  - teleport cross-playfield `Playfield` handoff lookup

## PlayfieldContentDataProvider Progress

- `PlayfieldContentDataProvider` sits behind `PlayfieldRuntimeSystems`.
- Raw content data selection and filtering moved behind the provider:
  - static dynel definitions
  - vendor statel filtering
  - collision-capable statel filtering
- `Playfield` still owns runtime object construction, spawn order, packet emission, and collision runtime behavior.

## ZoneClientSessionLifecycleCoordinator Progress

- `ZoneClientSessionLifecycleCoordinator` is owned by `ZoneClient`.
- The coordinator models session phases:
  - `Connected`
  - `CharacterLoading`
  - `PlayfieldLoading`
  - `ReadyBlock`
  - `FullCharacterBoundary`
  - `CharInPlay`
  - `InPlay`
  - `Zoning`
  - `Disconnecting`
- The coordinator owns allowed transition rules and rejects invalid transitions.
- Duplicate same-phase transitions remain no-op.
- Phase ownership currently uses named coordinator methods for:
  - playfield-loading and zoning-exit handoff
  - ready block entry
  - full-character boundary
  - CharInPlay visibility entry
  - InPlay completion
  - zoning entry
  - disconnecting/session dispose
- Packet emission remains outside the coordinator for this checkpoint.
- The coordinator remains phase-only and must not own combat, movement, GenericCmd, inventory, org, database import, capture tooling, private-city ready/init internals, or packet serialization behavior.

## Guardrails

- Playfield lifecycle tests guard private-city ready/init order, same-playfield visibility order, robot death/corpse/despawn order, content-module ownership, content-data-provider ownership, dynel-registry ownership, and ZoneClient lifecycle ownership.
- The ZoneClient lifecycle guardrail asserts:
  - transitions are owned by `ZoneClientSessionLifecycleCoordinator`
  - invalid transitions are rejected
  - duplicate same-phase transitions are no-op
  - final phase ownership uses named coordinator methods
  - packet emission stays outside the coordinator
  - packet/runtime surfaces do not own lifecycle enum transition rules directly
  - teleport/redirection mechanics stay outside the coordinator
  - private-city ready/init packet construction stays outside the coordinator
  - SCFU/CharInPlay broadcast stays outside the coordinator
  - engine/client disposal mechanics stay outside the coordinator

## Remaining Major Architecture Systems

- Packet sequencing remains outside the lifecycle coordinator intentionally.
- Teleport/redirection mechanics remain in `Playfield`.
- Private-city ready/init packet construction remains in the existing Playfield/runtime-systems coordinator path.
- SCFU/CharInPlay broadcast mechanics remain in `Playfield`.
- Engine/client disposal mechanics remain in `ZoneClient`.
- Continue separating Playfield packet lifecycle orchestration into focused coordinators once guardrails exist.
- Continue moving safe static/captured content data loading behind provider boundaries.
- Keep runtime systems behind `PlayfieldRuntimeSystems` and object lookup behind `PlayfieldDynelRegistry`.
- Keep content modules content-only.

## Recommended Next Major Step

Choose the next major architecture system based on risk:

- Packet sequencing coordinator: highest fit if the next priority is reducing packet-order regression risk around login, zoning, ready block, FullCharacter, and CharInPlay. This should keep packet construction/send calls external at first and only coordinate named sequence steps.
- Inventory/container runtime service: highest fit if the next priority is reducing item/container/backpack/bank/corpse loot coupling. This is likely broader because inventory behavior touches persistence, packet updates, GenericCmd use paths, and loot flows.

Recommended order: move a guarded packet sequencing coordinator first, then start the inventory/container runtime service. Packet sequencing is narrower and directly builds on the completed lifecycle phase ownership boundary.
