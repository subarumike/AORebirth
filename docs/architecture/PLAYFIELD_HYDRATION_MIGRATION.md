# Playfield Hydration Migration

## Scope and current authority

This tranche implements Stage 0 characterization and the Stage 1 compatibility
boundary only. The current mixed loader remains the sole production authority.
No DAO-backed hydrator, shadow execution, allowlist, replacement materializer,
new configuration switch, database migration, or service endpoint is present.

The production composition seam is `ZoneServer.CreateOwnedPlayfield`. It now
delegates once through `PlayfieldInstantiationCoordinator` in `Legacy` mode to
`LegacyPlayfieldRuntimeMaterializer`, which invokes the pre-existing
`new Playfield(...)` construction once. `RuntimeOwnershipRegistry` remains the
owner and continues to ensure one live instance per playfield instance key.

The complete source inventory and current precedence are recorded in
`PLAYFIELD_HYDRATION_SOURCE_INVENTORY.md`.

## Target architecture

```text
CellAO/AORebirth DAO reads ─┐
JSON and CSV sources ───────┤
Hardcoded compatibility ────┤
Accepted captured evidence ─┤
Official resource evidence ─┘
             │
             ▼
    source-specific adapters
             │
             ▼
 IPlayfieldDefinitionHydrator
             │
             ▼
 HydratedPlayfieldDefinition
  ├─ validation diagnostics
  ├─ record/category provenance
  ├─ deterministic canonical form
  └─ SHA-256 canonical digest
             │
             ▼
 IPlayfieldRuntimeMaterializer
             │
             ▼
      runtime IPlayfield
```

ZoneEngine will depend on the contracts above the concrete source adapters.
Database entity classes must not become the hydration result, and concrete DAO
calls must not be moved into `Playfield.cs`.

## Hydration versus materialization

Hydration is a side-effect-free read and validation of static configuration. A
hydrated definition may contain playfield/resource identity, geometry
references, spawn descriptors, template references, static transforms,
vendor/service definitions, doors, teleports, behavior-policy references,
quest/service hooks, and respawn policies when the source proves them.

Materialization creates and registers live NPCs, doors, vendors, service
objects, timers, buses, and runtime coordinators. Active players, current
HP/nano, aggro, corpses, loot rolls, live timers, temporary identities,
transient combat, current pathing, and service sessions remain runtime state.
The Stage 1 validator rejects those runtime-only names and runtime provenance;
the canonicalizer does not silently ignore them.

The definition and canonical formats are independently and explicitly versioned.
Canonical format version 1 accepts only definition format version 1 and emits
both version numbers. Its canonical form uses an explicit property allowlist,
ordinal key ordering, ordinal collection-member ordering, invariant integer
formatting, and invariant round-trip (`"R"`) formatting for `float` values. All
current definition properties are included; none are silently excluded. The
canonicalizer runs definition validation and rejects duplicate identities,
runtime-only values, runtime provenance, and every other validation error before
emitting bytes. Validation warnings remain explicit canonical content. No
tolerance is applied to positions or headings. Canonical digests are lowercase
SHA-256 over the UTF-8 canonical representation. Hydration-generated timestamps,
process IDs, active entity references, and runtime services are not part of the
model.

## Legacy compatibility and source precedence

Stage 1 delegates to the existing constructor; it does not reproduce its source
logic. The legacy constructor still builds `PlayfieldRuntimeSystems`, resolves
and registers statels, materializes database spawns, registers captured and
hardcoded content modules, materializes vendors and static dynels, refreshes the
dynel registry, and starts the existing heartbeat. Existing loader and branch
order remains authoritative:

1. `playfields.dat` base metadata and statels.
2. `Playfields.xml` catalog metadata.
3. Database mob spawns with the existing suppression policy.
4. Registered captured and hardcoded content modules in their current order.
5. Database and RDB-backed vendors.
6. Database static dynels.
7. Runtime dynel-registry refresh.

Item-derived collision actions, teleport overrides, PF127 compatibility
mappings, generated catalogs, accepted capture definitions, official geometry,
and playfield-specific branches retain their documented position inside those
legacy paths. No source was removed, reordered, copied into a new authority, or
written back to the database.

The `Playfield` constructor is still side-effectful and cannot safely expose a
complete static definition without first extracting source-specific,
side-effect-free adapters. Therefore Stage 1 wraps the narrow construction seam
but does not attempt to wrap individual internal source reads or produce a
definition from the live object.

## Provenance requirements

Every future adapter contribution must record, at definition or record/category
scope:

- source type and concrete source identity;
- source digest when the source supplies or permits one;
- adapter/provider name;
- contribution order;
- accepted, compatibility, unresolved, or rejected classification;
- warnings and conflicts without converting them into accepted facts.

Unknown provenance remains `Unresolved`. File names, raw hits, proximity, ASCII,
or structural parser evidence do not become runtime identity or authority
without the repository's acceptance bridge. Historical accepted evidence is
retained; raw captures are never bulk-promoted merely because they are present.

## Mode progression and all-or-nothing rule

The planned modes are:

- `Legacy`: current and only functioning/default mode.
- `Shadow`: future side-effect-free hydration and comparison; not implemented.
- `AllowList`: future per-playfield authority after acceptance; not implemented.

`Shadow` and `AllowList` fail closed in the Stage 1 coordinator. No environment
or production setting can activate them, and no experimental provider is
constructed or called during normal startup.

Future authoritative hydration must be transactional at the instance boundary:

```text
hydrate complete definition
validate complete definition
materialize complete definition
publish complete runtime ownership
```

A single playfield instantiation must never combine a partial legacy result with
a partial hydrated result. If hydration fails before materialization, the whole
legacy path may be selected. If materialization begins and fails, the partial
runtime must be disposed before any complete legacy retry.

## Rollback

`Legacy` remains the rollback target until each migrated playfield and category
has accepted shadow parity and relevant live acceptance. Rollback selects the
complete legacy construction path before materialization; it does not merge
sources mid-instance. Existing providers, historical evidence, generated
catalogs, and compatibility rules remain versioned and available throughout the
migration.

Database evolution, if later approved, is additive: create new versioned
structures, backfill with reversible tooling, validate parity, and retain the
old read path until rollback is proven. This tranche adds no schema and no
database writes. Destructive SQL, normalization writes, and implicit pruning
are prohibited.

## Consultation interface

A later read-only consultation interface should query the exact accepted
hydrated definition used by ZoneEngine and expose its format/revision,
diagnostics, canonical digest, and provenance. It must not reinterpret DAO
tables independently. Editing is deferred until authentication, authorization,
audit logging, validation, concurrency control, and rollback are separately
designed. No HTTP endpoint is part of Stages 0 or 1.

## Deferred service boundaries

World/Zone sharding is deferred until deterministic hydration is stable. The
monolith must first define zone identity, instance identity, ownership lease,
fencing token, lifecycle, routing, health, recovery, and shutdown semantics.
Splitting processes before those semantics exist could create two owners for one
zone instance.

Economy extraction is deferred because trade and inventory mutations first need
one transactional monolith boundary with idempotency, ownership and item-policy
validation, concurrency control, authoritative audit records, duplicate-request
rejection, and crash recovery. This tranche does not change trade, inventory, or
economic persistence.

## Exact Stage 2 entry criteria

Stage 2 may begin only when all of the following are true:

1. The baseline is clean, or every pre-existing failure is separately
   documented and proven unrelated.
2. All relevant build, startup, playfield, spawn, combat, vendor, teleport,
   DAO, AOtomation, and offline smoke tests pass, except those same documented
   pre-existing failures.
3. Canonical serialization and SHA-256 digest behavior are deterministic under
   repeated runs and reordered source collections.
4. Stage 1 introduces no production playfield, packet, spawn, database, startup,
   source-precedence, or runtime-authority behavior change.
5. A side-effect-free DAO read path is identified that performs no writes,
   runtime-object construction, packets, spawn/service registration, or global
   collection mutation.
6. The representative cohort is selected and its current coverage and gaps are
   recorded.
7. Every current source path, consumer, precedence rule, collision, and runtime
   side effect relevant to the cohort is documented.
8. Complete rollback to `Legacy` remains available before materialization.
9. The Stage 0 inventory and Stage 1 seam receive review before any DAO shadow
   hydrator is added.

Until these criteria are accepted, the migration stops at Stage 1.
