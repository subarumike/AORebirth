# AO Spawn Population Reconstruction

`Tools\spawn_population_reconstruction.py` is the deterministic, non-mutating
population layer over the governed official ACG placement corpus, accepted
captured NPC observations, the phase-aware placement resolver, and the official
enemy-archetype census.

Run the governed Windows workflow with:

```cmd
cmd /d /c Tools\run_spawn_population_reconstruction.cmd
cmd /d /c Tools\run_spawn_population_reconstruction.cmd --check
```

## Permanent identity model

```text
ACGHash          = static placement/spawn-policy identity
MonsterData      = server-selected runtime model/archetype identity
Runtime Identity = transient spawned-instance identity
```

The effective AO client resources contain no direct or indirect static
`ACGHash -> MonsterData` bridge. The reconstruction does not search for one,
and no generated association implies that ACGHash was transmitted with a
runtime NPC.

## Population hierarchy

```text
visual / creature archetype
  -> contextual runtime variant
  -> spawn population / placement group
  -> individual ACG placements
  -> transient runtime instances
```

Names, levels, loot, combat, lifecycle, and movement are contextual evidence.
They do not alter visual-archetype identity. Runtime identity is qualified by
capture and is never reused as persistent population identity.

## Topology

Every one of the 32,805 placements retains its ACGHash, official playfield and
district, exact coordinates, encoded heading, spawn-policy fields, additional
points, extensions, source provenance, and explicit absence of a proven native
parent/generator ID.

Two grouping layers remain separate:

- `officialGroupIds` groups one shared ACG policy tag inside one official
  district. This is direct structural evidence, not creature identity.
- `derivedSpatialClusterId` is a deterministic 25-metre three-dimensional
  connected component within that structural group. It is heuristic secondary
  topology and never becomes an official semantic field.

## Association scopes

- `exact-placement`: one proven base playfield, one exact coordinate candidate,
  and no resolver conflict. The method is position correlation and
  `explicitIdBridge=false`.
- `local-population`: one topology population plus a governed existing overlay,
  or repeated stable MonsterData/archetype observations across captures with
  changing transient identities. This never claims individual row ownership.
- `playfield-population`: the capture proves a contextual population in the
  retained resource-playfield session but not a local cluster owner.
- `unassociated`: no retained official playfield context.
- `conflict`: the phase-aware resolver records contradictory epoch, mapping, or
  lineage evidence.

Spatial proximity by itself remains a blocked candidate. A moved position is
never substituted for spawn; the catalog retains first-observed/SCFU position
and captured movement extent separately.

## Readiness

Each catalog population independently reports:

- visual readiness;
- population-identity readiness;
- level/context readiness;
- combat evidence;
- loot evidence;
- respawn readiness;
- exact-placement readiness.

Finite loot observations are samples, not complete probabilities. Population
identity is implementation-ready only at direct or strong exact/local scope.
Corroborating population records remain useful evidence but fail closed for
automatic runtime promotion.

## Outputs

Bulk catalogs use deterministic gzip JSON under
`docs/generated/spawn_populations`:

- `spawn-topology.json.gz`
- `runtime-population-observations.json.gz`
- `runtime-populations.json.gz`
- `spawn-population-catalog.json.gz`
- `implementation-inventory.json.gz`

The summary, archetype-reuse report, Leet/PF4582/Borealis studies, and human
report remain directly readable. Raw captures, resource databases, production
NPC definitions, placement activation, and database state are never modified.
