# Playfield Object Materialization Runtime Boundary

Date: 2026-07-06

## Summary

`PlayfieldObjectMaterializationRuntimeService` now owns the startup object materialization order for a playfield. This moves the cohesive orchestration around DB mob spawn iteration, content registration, vendor materialization, static dynel materialization, and final dynel-registry refresh out of `Playfield`.

This is an ownership boundary only. Runtime values, identity allocation, persistence behavior, object state, and packet order are unchanged.

## Service-Owned Sequence

The service preserves the constructor startup order:

1. Materialize DB mob spawns.
2. Skip content-suppressed DB rows.
3. Instantiate and activate each DB mob.
4. Attach KnuBot scripts after activation.
5. Register playfield content modules.
6. Resolve and spawn vendor statels.
7. Resolve and register static dynels.
8. Refresh the dynel registry.

## Intentionally Still Outside The Service

- DAO calls for mob spawn rows and mob spawn stats.
- Runtime object construction, including `StaticDynel`, `NPCController`, and mob instantiation.
- KnuBot script creation.
- Vendor spawning implementation.
- Packet construction, serialization, and emission.
- Combat, loot, item, stat, and persistence algorithms.

## Guardrail

`PlayfieldContentDataProviderOwnsStaticContentDataResolution` asserts the service owns only materialization order and loops, while `Playfield` keeps data-loading callbacks and construction callbacks.
