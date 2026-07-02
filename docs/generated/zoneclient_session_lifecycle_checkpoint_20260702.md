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
- Current lifecycle markers are wired around existing login, ready block, full-character, CharInPlay, in-play, zoning, and disconnect boundaries.
- Packet emission remains outside the coordinator for this checkpoint.
- The coordinator remains phase-only and must not own combat, movement, GenericCmd, inventory, org, database import, capture tooling, private-city ready/init internals, or packet serialization behavior.

## Guardrails

- Playfield lifecycle tests guard private-city ready/init order, same-playfield visibility order, robot death/corpse/despawn order, content-module ownership, content-data-provider ownership, dynel-registry ownership, and ZoneClient lifecycle ownership.
- The ZoneClient lifecycle guardrail asserts:
  - transitions are owned by `ZoneClientSessionLifecycleCoordinator`
  - invalid transitions are rejected
  - duplicate same-phase transitions are no-op
  - packet emission stays outside the coordinator
  - packet/runtime surfaces do not own lifecycle enum transition rules directly

## Remaining Major Architecture Systems

- Move safe ready/full-character/CharInPlay sequencing surfaces behind lifecycle coordination without changing packet emission.
- Continue separating Playfield packet lifecycle orchestration into focused coordinators once guardrails exist.
- Continue moving safe static/captured content data loading behind provider boundaries.
- Keep runtime systems behind `PlayfieldRuntimeSystems` and object lookup behind `PlayfieldDynelRegistry`.
- Keep content modules content-only.

## Recommended Next Major Step

Move safe ready/full-character/CharInPlay sequencing surfaces behind `ZoneClientSessionLifecycleCoordinator` without changing packet emission or packet order. The coordinator should own lifecycle sequencing decisions first; packet construction and send calls should remain in the existing handlers until a later guarded extraction.
