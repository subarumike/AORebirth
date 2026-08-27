# NPC Placement Identity Resolver

`Tools\npc_placement_identity_resolver.py` is the deterministic, fail-closed
audit between accepted captured NPC observations and the complete normalized
official placement corpus. It answers one question only: whether one captured
runtime NPC can be tied to one specific official placement using proven
evidence. It does not promote NPC behavior or modify runtime definitions.

Run the governed workflow with:

```cmd
cmd /d /c Tools\resolve_npc_placement_identity.cmd
```

Outputs are ignored build evidence under
`build-verify\npc-placement-identity-resolver`. Run the wrapper twice without
changing inputs to establish `DETERMINISTIC_REPEAT_RUN=YES`.

## Starting repository state

The task started from a clean `codex/npc-observation-harvester` worktree at
`f0c25485a17beaee53c99dd608809d1642ca5920`. The isolated task branch is
`codex/npc-placement-identity-resolver`. At task start, `origin/master` and the
merge base were both `000fb673dcd62bd6477848f655cecf975ea3225a`.
No reset, clean, stash, capture deletion, inventory prune, or raw-evidence
mutation was performed.

## Production result

The current 358-capture corpus contains no proven placement identity bridge:

```text
OFFICIAL_PLACEMENT_COUNT=32805
OFFICIAL_FIELDS_EXPANDED=146
DISTRICT_TRANSFORM_DECODED=NO
COORDINATE_SYSTEM_PROVEN=NO
RUNTIME_BASE_PLAYFIELD_MAPPING_PROVEN=NO
OBSERVATIONS_ANALYZED=3325
OBSERVATION_CLUSTERS=2988
UNIQUE_PROVEN_MATCHES=0
AMBIGUOUS_MATCHES=680
UNMATCHED_OBSERVATIONS=2365
CONFLICTING_MATCHES=280
HEURISTIC_MATCHES_PROMOTED=0
ACGHASH_USED_AS_RUNTIME_IDENTITY=NO
RUNTIME_NPC_DEFINITIONS_MODIFIED=NO
PROMOTION_ELIGIBILITY_GENERATED=3325
```

The ambiguity count is intentionally larger than the original harvester's 222.
The resolver retains radius candidates exposed by packet destination-playfield
proxies while refusing to call those proxies proven base-playfield mappings.
The 280 conflicts include 272 phase-aware rejections where a frozen capture-
level resource label conflicts with row-level runtime epochs, plus eight
observations in four repeated-lineage metadata-conflict clusters. No count was
improved by weakening proof.

## Exact official resource provenance

The governed source is the old-graphics `18.8.62_EP1` client resource database,
resource type `1000014`. The retained PF4582 provenance records the installed
client location as `Anarchy Online\cd_image\data\db\ResourceDatabase.dat`,
with the numbered segments and index alongside it:

| Resource file | SHA-256 |
| --- | --- |
| `ResourceDatabase.dat` | `3cabdede7b9b2468ed22f10f536fb2f7083ea05ed9483e2d96b22cf080d736a6` |
| `ResourceDatabase.dat.001` | `f8884a2c382ce7c95f20b4423567f176ed40675ba9ce8362527288712871ba73` |
| `ResourceDatabase.dat.002` | `2024021f966c3c8a8c083e01cbad2335ba33c19a1661a148060391755a608cc1` |
| `ResourceDatabase.idx` | `ba152f59096d5358f4d1b6511d3a3d264999e0a59f1ab7bf3a7cc18a4888c273` |

The exact extraction path is:

```text
ResourceDatabase segments + index
  -> external official input manifest, resource inventory, import index,
     six global reports, and 630 resource_*.json shards
  -> Tools/import_official_playfield_placements.py
  -> docs/reference/playfields/official-placement-source-manifest.json
  -> docs/generated/playfields/official-placement-index.json
  -> docs/generated/playfields/placements/pf_<resource-instance>.json
```

AORebirth hash-pins but does not track the raw database bytes, upstream parser,
six global report payloads, or 630 upstream source shards. The importer points
to that read-only external extraction source, but this audit did not cross the
AORebirth workspace boundary. PF4582 is the sole local structured upstream
snapshot, and its evidence directory intentionally contains no official binary
or bulk scanner output.

The corpus has 630 resource instances. The source manifest proves, for those
630 extraction controls only, that type-`1000014` resource instance `N` is
static playfield ID `N`, with zero conflicts. It has 627 parsed resources,
4,146 districts, and 32,805 `HashSpawnPoint_t` records. PF103, PF615, and PF4805
remain parser-limited. Resource format versions include 88 parsed format-5,
307 parsed format-6, and 232 parsed format-7 resources.

## Retained hierarchy and identity boundary

The proven native hierarchy is:

```text
ResourceDatabase type/instance
  -> PlayfieldDistrictInfo_t
  -> DistrictData_t
  -> HashSpawnPoint_t
  -> ACGHash_t
```

Each expanded placement retains the complete normalized source record and adds
an evidence-aware view of:

- static resource instance and playfield ID;
- normalized district and record ordinals;
- direct source `PositionX/Y/Z` values;
- encoded rotation midpoint and width;
- level range, radius, assistance radius, respawn values, and optional flags;
- source resource/record offsets, serialized sizes, and hashes;
- record-owned `AdditionalPoints`, extensions, field-presence bits, and source
  unknown fields;
- AORebirth overlay fields in a separate, explicitly non-native section.

`OfficialSpawnRecordId` and `OfficialDistrictId` are deterministic normalized
keys, not original Funcom runtime identifiers. PF4582 `SourceNpcId` is likewise
an AORebirth source key. The retained official structure exposes no proven
template ID, MonsterData join, archive/object ID, parent, spawn group, runtime
instance, path, patrol, cell/grid mapping, origin contract, or transform
matrix. ACGHash is retained only as a packed four-byte scalar/tag and is never
used as runtime identity.

## Discarded and opaque field audit

The current normalized importer preserves every expected decoded source key
except `AcgHashNativeUInt32Hex`, which is validated upstream but not emitted;
it is exactly derivable from the retained native uint32. Completeness is not
fully provable because validation accepts source-key supersets while resource,
district, and record normalization use fixed projections. Unexpected upstream
keys could be silently dropped, and the upstream shards required to audit that
possibility are not tracked here.

Known raw payload gaps are:

- `TrailingOpaqueRegion`: 622 resources, 507,976 bytes total, with only
  offset/length/SHA retained;
- `RecordAllocationSlack`: 45 bytes total (36 in PF111 and 9 in PF9080), again
  with only metadata retained;
- PF103/PF615 undecoded envelopes and PF4805's unsupported extension-key tail;
- serialized resource, district, and placement bytes, which are represented by
  decoded projections and hashes rather than byte payloads.

District unknowns remain explicit: `Centre`, nine `LevelOrStyleU16` values,
two range pairs, legacy/unknown integers, `RotationPoints`, `SecondaryHashes`,
`ShortPairs`, and `SpawnInfo`. Corpus-wide structural facts do not provide
semantics: 59,239 rotation points occur in 2,424 districts; 7,203 SpawnInfo
entries occur in 1,479; SecondaryHashes is empty everywhere; and only 205
within-district tag-set intersections connect SpawnInfo tags to placement tags.
There is no universal ordinal, pointer, or identity join. The resolver decoded
no new production field because the bytes needed to justify a new decoding are
absent.

## Coordinate-space analysis

The importer performs no transform; it copies source `PositionX/Y/Z` directly.
The resolver measures all 48 composed axis-order/sign combinations plus direct-
axis scales, quantization steps, district-centre add/subtract hypotheses, and
origin yaw rotations. Fixed/playfield offsets, unknown district rotations,
cell/grid transforms, and instance transforms are emitted as explicitly
untestable candidates rather than fitted per NPC.

The best direct `X/Y/Z` diagnostic over 3,153 observations and 19 candidate
partitions was:

```text
TRANSFORM_NAME=axis-order-xyz
SAMPLES=3153
PLAYFIELDS=19
MEDIAN_ERROR=1.025336
P95_ERROR=59.194071
MAX_ERROR=711.344777
EXACT_MATCHES=0
WITHIN_0_1=185
WITHIN_0_5=1052
WITHIN_1_0=1555
```

The large outliers are not evidence for an axis swap. Two concrete multi-zone
capture diagnostics show frozen-label mispartitioning:

| Capture resource label | Row runtime | Observations | Label median nearest error | Runtime-number partition median | Runtime partition within 1m |
| --- | --- | ---: | ---: | ---: | ---: |
| PF4310 | 4311 | 8 | 458.230735 | 0.349002 | 8 |
| PF4677 | 4310 | 71 | 756.099141 | 0.700221 | 49 |

This strongly corroborates direct X/Y/Z coordinates for those static numeric
partitions. It does not prove a universal transform: every measurement still
chooses an unpaired nearest neighbor, there are zero exact matches, and no
placement-specific identifier independently establishes correspondence.

The two-axis projection audit adds a narrower result. Across all diagnostic
partitions, 389 observations exactly match one official X/Z float32 pair; XY
and YZ have zero. PF4582 contributes 373 X/Z matches spanning 74 official
records and 16 captures, with 348 AORebirth overlay-name agreements. The
remaining absolute Y error has median `0.253548`, p95 `0.471052`, and maximum
`0.611502`. This is strong corroboration that PF4582 uses the same X/Z axes and
scale. It is not placement identity: the projection discards Y, no evidence
proves a ground-height normalization, and 302 of those matches have official
radius below one unit (`253` radius zero, `25` approximately `0.001`, and `24`
approximately `0.01`). The resolver therefore does not convert an exact X/Z
pair into an exact three-dimensional match.

`District.UnknownFields.Centre` is a decoded vector, but no evidence defines it
as an origin or district-to-world translation. Therefore both
`COORDINATE_SYSTEM_PROVEN` and `DISTRICT_TRANSFORM_DECODED` remain `NO`.

## Runtime and base-playfield identity

AOSharp exposes runtime `Playfield.Identity` and model
`Playfield.ModelIdentity` separately. Current capture artifacts do not sample
them atomically:

- `capture_info.resourcePlayfieldId` is the model instance frozen at session
  start;
- `capture_info.playfieldId` is the latest `PlayfieldInit`/final runtime value;
- SCFU rows retain runtime `PlayfieldId`, but no model identity or official
  district;
- the model identity's type is discarded in the capture output.

The conditional bridge is valid only when the same zone epoch retains runtime
`R`, the full model identity `(1000014,N)`, and the governed 630/630 static
relationship. No accepted production observation retains that complete chain,
so production has zero proven runtime-to-base mappings and zero runtime-to-
district mappings.

`N3Teleport` can directly pair a runtime `ChangePlayfield` with a destination
identity, but that identity is context-dependent (static proxy, building, or
ACG entrance). A type-51102 destination proxy is retained as corroborating
candidate-partition evidence only; it is not declared equivalent to a
type-1000014 official partition.

The original harvester applied one capture-folder/model label to every SCFU row.
That is unsafe across zoning. The current evidence includes 272 observation-
level phase conflicts that the resolver refuses instead of reconciling against
the wrong static partition.

## Repeated observation clusters

The conservative lineage key is resource label, row runtime playfield, and
exact runtime identity. MonsterData, appearance signature, breed/gender/race,
name, and position are evaluated after grouping so contradictory metadata
cannot evade detection by splitting into separate clusters. Name alone can
never merge clusters. Position history and movement/lifecycle continuity are
retained after clustering.

Current totals:

- 2,988 clusters from 3,325 observations;
- 222 repeated, multi-capture clusters;
- 2,132 clusters with a stable position under the strict 0.1-unit bound;
- maximum five observations/captures in one cluster;
- four internal metadata-conflict clusters covering eight observations, all
  failed closed.

Repeated observations do not eliminate the missing official identity field.
They can corroborate stable appearance/position but cannot convert a radius or
nearest-neighbor candidate into a proven placement.

## Required populations

### Borealis backyard capture

The capture starts with resource label PF3081 and later zones to runtime
2130084. The zoning packet pairs a type-51102 destination proxy `954` with that
runtime, so applying PF3081 globally is rejected. Proxy PF954 is used only for
candidate diagnostics.

An initial pre-zone Guide snapshot is near PF3081's sole official placement,
but it has a different runtime identity and is not an SCFU observation in the
harvested set. The required HeadMesh/texture rows below are post-zone PF954
candidates. They must not be merged with the initial Guide solely by name.

- Guide runtime position: `(232.512512, 6.01, 75.99942)`.
- Guide appearance: HeadMesh `40635`; textures
  `42239,42260,42240,42261`.
- Nearest PF954 district-2 record:
  `18.8.62_EP1:1000014:954:district-2:record-1` at
  `(232.548400879, 6.399409771, 75.196914673)`, distance `0.892717`, radius
  `1.0`.
- Guard runtime position: `(247.061264, 5.81000042, 76.54147)`.
- Guard appearance: HeadMesh `40111`; textures
  `30848,42260,30831,42261`.
- Nearest PF954 district-2 record:
  `18.8.62_EP1:1000014:954:district-2:record-0` at
  `(246.281097412, 5.800110340, 76.547546387)`, distance `0.780253`, radius
  `1.0`.

Each has one radius candidate and preserved appearance, but neither has an
exact coordinate, proven proxy-to-base equivalence, proven coordinate
transform, official appearance field, or placement-specific identifier. Both
remain `ambiguous`, never `unique-proven`.

### PF4582 ICC Shuttleport

The generic corpus contains 207 official records across two districts; the
specialized accepted source contains 206 placements. The current tracked
runtime-evidence ledger has 35 `SourceNpcId`/profile rows, but those govern
AORebirth definitions and do not join captured `SimpleChar` identities to
official records.

The requested 25-active/181-blocked premise is retained in the 2026-08-25
reconciliation evidence, but it does not match the current accepted baseline's
generated catalog: the current report says 199 eligible and 7 blocked across
the specialized 206 records, split into 35 explicit and 164 generated-profile
active records. The official 207-row overlay has 199 authorized and 8 blocked
including NCNN. The resolver records this discrepancy without changing either
catalog. Neither gate supplies a capture-to-placement identity bridge.

The extensive capture set contains 1,474 observations in
1,323 clusters: 189 ambiguous radius candidates, 1,285 unmatched, zero
conflicts, and zero unique-proven matches. ACGHash remains excluded.

### Three additional capture-rich playfields

| Capture resource population | Observations | Clusters | Unique | Ambiguous | Unmatched | Phase conflicts |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PF4677 | 87 | 79 | 0 | 12 | 4 | 71 |
| PF127 | 50 | 50 | 0 | 10 | 40 | 0 |
| PF4310 | 33 | 33 | 0 | 11 | 13 | 9 |

These populations independently confirm that coordinate proximity is useful
for diagnosis but not identity. The Nascence captures also prove that one
capture-level resource label cannot safely partition a multi-zone SCFU corpus.

## Evidence tiers and promotion

- Proven evidence may directly participate in `unique-proven`: same-epoch base
  mapping, proven transform, exact transformed coordinate, or a placement-
  specific identifier.
- Corroborating evidence can reject contradictions and strengthen a result but
  cannot independently choose identity: packet destination proxy,
  MonsterData, HeadMesh, textures/meshes, level, breed/profession, heading, and
  name.
- Heuristic evidence never enables promotion: nearest placement, radius
  containment, name similarity, visual similarity, and ACGHash.

All 3,325 promotion-eligibility records are blocked. `promotionReady` can only
be true for `unique-proven`, and production has zero such records. The workflow
has no code path that edits production NPC definitions.

## Deterministic outputs

| File | Purpose |
| --- | --- |
| `official-placement-expanded.json` | Full normalized source structure, proven IDs, opaque/retention audit, and 146-field-path inventory |
| `coordinate-transform-analysis.json` | Quantitative metrics and rejection reason for every transform candidate |
| `runtime-playfield-mapping.json` | Phase-aware runtime/model/proxy evidence and mapping blockers |
| `observation-clusters.json` | Conservative repeated-observation clusters and position variance |
| `placement-candidates.json` | Exact, radius, nearest-diagnostic, and corroborating-elimination records |
| `placement-resolution.json` | Evidence-tiered final resolution for every observation |
| `unique-proven.json` | Production unique-proven set; currently empty |
| `ambiguous.json` | Fail-closed candidate sets |
| `unmatched.json` | Observations outside candidate regions |
| `conflicts.json` | Phase/mapping or cluster contradictions |
| `promotion-eligibility.json` | Non-mutating promotion gates for all observations |
| `summary.json` | Acceptance fields, population totals, and deterministic digest |

The 25 focused tests include all twelve mandatory refusal cases, the full-model-
identity type and observation-zone-epoch boundaries, repeated-lineage
contradiction detection, and six
isolated positive mechanics fixtures. Positive fixtures prove decoder/resolver
mechanics only; they do not fabricate a production match.
