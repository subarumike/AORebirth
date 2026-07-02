# Playfield Runtime And Data Provider Checkpoint

Date: 2026-07-02

## Scope

This checkpoint records the completed PlayfieldDynelRegistry and first PlayfieldContentDataProvider boundary work. It is an architecture checkpoint only. It does not document gameplay behavior changes.

## PlayfieldDynelRegistry Progress

- `PlayfieldDynelRegistry` now sits behind `PlayfieldRuntimeSystems`.
- Safe typed lookup surfaces moved behind the registry:
  - identity lookup
  - typed identity lookup
  - dynel range lookup
  - character range lookup
  - current-playfield character views
  - concrete `Character` views for existing broadcast paths
  - static dynel views
  - statel, terminal, and door views
- Player visibility and safe local character loops now use registry-backed views.
- Remaining direct `Pool` usage in `Playfield` is guarded as explicit global or cross-playfield exceptions:
  - `DisconnectAllClients`
  - `NumberOfDynels`
  - `NumberOfPlayers`
  - teleport cross-playfield `Playfield` handoff lookup

## PlayfieldContentDataProvider Progress

- `PlayfieldContentDataProvider` now sits behind `PlayfieldRuntimeSystems`.
- Raw static content data selection moved behind the provider:
  - statel resolution from `PlayfieldLoader`
  - static dynel DB row selection
  - static dynel stat payload deserialization
  - static dynel definition assembly
  - vendor statel filtering
  - collision-capable statel filtering
- `Playfield` still owns runtime construction and lifecycle behavior:
  - vendor runtime spawning call
  - `StaticDynel` runtime object construction
  - statel collision range checks
  - statel event execution
  - constructor/load order

## Guardrails Added

- Content modules remain content-only and cannot own runtime systems.
- Known content modules must be registered exactly once through the coordinator path.
- `PlayfieldDynelRegistry` ownership and safe lookup delegation are guarded.
- Remaining direct `Pool` usage in `Playfield` is limited to named exceptions.
- `PlayfieldContentDataProvider` owns content data selection/filtering but must not:
  - emit packets
  - construct runtime dynels
  - spawn vendors
  - own combat, corpse, GenericCmd, inventory, org, private-city ready/init, or capture-tooling behavior

## Remaining Direct Playfield Responsibilities

- Session/lifecycle ownership.
- Connection-facing packet orchestration.
- Teleport and cross-playfield handoff behavior.
- Combat tick entry points and runtime combat state.
- Corpse lifecycle scheduling and loot/credit handoff.
- Runtime object construction for vendors and static dynels.
- Statel collision runtime checks and event execution.
- Mob spawn runtime construction.
- Private-city ready/init sequencing through the existing coordinator.

## Recommended Next Major System

The next major architecture target should be a `ZoneClient` or session lifecycle coordinator. The current remaining high-risk coupling is connection/session orchestration mixed with packet emission and playfield lifecycle entry points. Extracting a coordinator around login/zoning/session-ready flow should happen before additional broad gameplay refactors.
