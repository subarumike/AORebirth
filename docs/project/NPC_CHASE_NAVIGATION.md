# Global NPC Chase Navigation

## Status and ownership

Geometry-aware hostile-NPC chase navigation is a global ZoneEngine capability owned by `ZoneEngine.Core.Navigation`. PF127/resource `127` is the first enabled playfield provider, and Vergil Aeneid is the first end-to-end evidence case. Every hostile NPC that reaches the shared `NPCRuntimeService` combat path in PF127 inherits the capability; there is no Vergil name, monster-data, identity, or coordinate dependency in the navigation subsystem.

Other playfields continue to use their existing direct `NPCController.Follow` behavior. They explicitly advertise `Unsupported` until authoritative collision or navigation input is promoted. PF127 reports `Unavailable` and fails closed if its expected geometry cannot be loaded.

This implementation is not a claim of official AO pathfinding parity or a game-wide navmesh.

## Responsibility boundaries

| Responsibility | Owner |
| --- | --- |
| Pursue, hold, attack, cancel, leash, death, and encounter policy | Existing `NPCRuntimeService` and `NpcCombatTickCoordinator` |
| Playfield capability and authoritative segment checks | `IPlayfieldChaseNavigationProvider` |
| Deterministic bounded route search | `BoundedGridChaseRoutePlanner` |
| Active waypoint selection, segment revalidation, deviation, and stuck detection | `NpcChaseRouteFollower` |
| Per-NPC route, target, geometry version, failure, retry, and invalidation state | `NpcChaseNavigationRuntimeService` |
| Speed, movement cadence, orientation, visibility, and client synchronization | Existing `NPCController.MoveTo`/`DoFollow` pipeline |

The combat coordinator asks the shared movement service to pursue. The navigation runtime first checks the direct segment. A clear segment uses the normal direct movement path without running A*. A blocked segment requests a route through the playfield provider. The route follower supplies only a currently collision-valid next destination to the existing controller. Damage remains gated until the established range and LOS/obstruction checks pass.

Patrol replay, DB-authored patrol waypoints, pet commands, and ordinary return-to-spawn behavior remain separate existing movement modes. The captured Cleaning Robot stop-distance rule remains a combat-policy exception, but its PF127 obstruction handling goes through the same navigation runtime.

## PF127 provider

`Pf127ChaseNavigationProvider` consumes the promoted, fail-closed `pf127-geometry.json` collision asset and its source SHA-256 geometry version. It does not build or maintain a competing collision model.

PF127 collision evidence proves blocking surfaces but does not currently provide a reliable walkable-floor projection. The first provider therefore derives a bounded same-elevation X/Z search slice from the NPC's authoritative live Y and validates every candidate edge against the promoted triangle geometry. Six collision probes are used for each movement segment: center and both `0.35`-unit radius offsets at `0.15` and `1.20` units above the live elevation. Plans whose endpoints differ by more than `1.50` Y units fail as unreachable. Cross-elevation, stairs, drops, and floor-connectivity routing remain unsupported until authoritative floor or navigation evidence is promoted.

## Search and following limits

The production search limits are deterministic and explicit:

- grid cell size: `2.5` units;
- detour margin around the start/goal bounds: `32.0` units;
- maximum start-to-goal distance: `160.0` units;
- maximum expanded nodes: `4096`;
- maximum collision segment checks: `32768`;
- goal connection distance: `10.0` units;
- maximum vertical step: `1.5` units;
- maximum route-smoothing checks: `256`.

Route planning allocates only when a route or bounded retry is required. Direct pursuit reuses per-NPC state and does not rebuild geometry or allocate a new route state on each combat evaluation.

Route-following limits are:

- evaluation throttle: `100 ms`;
- waypoint arrival distance: `0.75` units;
- maximum route deviation: `4.0` units;
- meaningful progress distance: `0.35` units;
- stuck timeout: `2.5 seconds`.

The controller clamps movement to the current destination, while navigation validates the complete direct or waypoint segment before issuing `MoveTo`; a large simulation step therefore cannot use an unvalidated wall-crossing shortcut.

## Replanning, failure, and cleanup

A route is planned or invalidated when no route exists and direct pursuit is blocked, the target identity changes, the target moves more than `3.0` units from the route sample, the geometry version changes, the NPC deviates more than `4.0` units, the active segment becomes blocked, the NPC remains stuck for `2.5 seconds`, or the route completes without normal chase/combat eligibility being restored. Direct-path restoration immediately returns control to direct pursuit. Direct destinations refresh only after `1.0` unit of meaningful movement.

Failed requests store NPC start, target identity/position, geometry version, status, and retry time. An identical failure is suppressed for `2 seconds`; movement of the NPC by more than `1.0` unit, material target movement, target replacement, geometry change, or expiry of the retry interval permits a new bounded request. `Unreachable`, `SearchLimitReached`, `Unavailable`, and `Unsupported` remain distinct results. No route means hold: no teleport, penetration, attack through the obstruction, uncontrolled movement, per-tick search, or noisy per-tick logging.

The shared lifecycle owner clears route state on target loss/replacement, combat cancellation, NPC death, corpse transition, despawn, leash return, encounter reset, playfield removal/reset, and runtime disposal. New respawn identities start without inherited route state.

## Validation coverage

The focused navigation suite passes `29/29`. It covers direct and obstructed pursuit, provider capability states, collision-valid route emission, existing movement-pipeline use, target and geometry invalidation, deviation/stuck recovery, stable unreachable behavior, retry suppression, lifecycle cleanup, respawn isolation, large-step tunneling prevention, direct-path restoration, search bounds, and unchanged combat damage ownership.

PF127 tests load the promoted geometry through the generic provider, prove the representative Vergil wall blocks direct pursuit, calculate a bounded collision-valid route, follow it through the shared state machine, and restore a clear direct combat path. Independent regressions pass: PF127 collision/LOS `17/17`, Abmouth/Vergil `19/19`, PF127 geometry-only safe mode `9/9`, and capture runtime safety `6/6`. The lifecycle assembly remains at its established `52/58` baseline with the same six unrelated session/packet ownership guardrail failures. AORebirth.Core and ZoneEngine Debug builds pass, as do the capture plugin/analyzer builds and analyzer/lifecycle-decoder self-tests. Private live validation is still required before claiming client-visible chase parity.

## Enabling another playfield

1. Promote authoritative, versioned collision or navigation evidence for the playfield.
2. Implement `IPlayfieldChaseNavigationProvider` using that authoritative representation.
3. Fail closed when the expected asset is absent or invalid.
4. Register the provider in `PlayfieldChaseNavigationProviderFactory`; do not edit an enemy profile or add enemy-specific chase code.
5. Add representative direct-block, route-validity, unreachable, lifecycle, and end-to-end combat tests.
6. Validate the route in the client without changing damage, cadence, weapon, aggro, or unrelated encounter behavior.
