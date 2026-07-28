# RK Mission ACG Interior Evidence — 2026-07-28

## Scope and result

This document records the evidence boundary for the first capture-backed RK
terminal-mission interior catalog.

The five finalized official-live captures now extract as
`complete_and_selectable`. Each produces a canonical
`ao-rebirth.mission-acg-layout` version 1 artifact with an exact generator
payload, entry correlation, physical layout slots, one evidence-bound objective,
and one decoded exit-boundary door. The deterministic catalog emitter converts
those five artifacts into five `CompleteSelectable` runtime bundle definitions.

This is a captured-layout catalog, not a reconstruction of AO's procedural
interior generator. It does not prove room-graph grammar, tile probabilities,
collision generation, or any interpretation of the opaque C79F generator
payload. Runtime mission-instance allocation, persistence, entry, spawning,
completion, and cleanup remain deferred.

No database schema or destructive database operation is part of this stage.

## Authoritative captures

The source directories are under
`tools-temp/AOSharpLiveCapture/bin/Debug/captures`.

Raw counts are packet observations inside the selected layout window. The SCFU
count includes the player. Physical counts are normalized, unique layout slots;
their NPC count excludes the player. Identity-only inventory/lifecycle records
remain ordered observations and are not physical slots.

| Capture | Mission / icon | Building / captured PF2 | Raw Door / Chest / SCFU | Physical Door / Chest / NPC / Objective | Generator payload SHA-256 |
|---|---|---|---:|---:|---|
| `20260728-001044` | Kill Person / `11330` | `00D734E2` / `0015F008` | 23 / 22 / 16 | 11 / 8 / 7 / 1 | `ffe4327ac8af0f0a41a04cff7fe53ecd40c55a027f10a2cda2cd2a8fc18f1269` |
| `20260728-003410` | Return Item / `11329` | `00D6FC77` / `0016C80E` | 64 / 44 / 59 | 21 / 15 / 21 / 1 | `f7f00e3344bd12f2d7d302761403c9c5b083fc8a181417c7f2c9748da501ff59` |
| `20260728-005042` | Find Item / `11337` | `00D6FC78` / `00169802` | 59 / 27 / 40 | 27 / 13 / 18 / 1 | `3cfe53d3a32b50679530bdfd5ff7572405eb8865f4ab0c13308c7bcd935bf431` |
| `20260728-010220` | Repair / `11342` | `00D734E5` / `0015F00F` | 26 / 15 / 29 | 17 / 12 / 19 / 1 | `e75f1326a72db6d42ddb5ebd72320338148193e6469e70b1c30b2d8a0f6d1926` |
| `20260728-012547` | Find Person / `11335` | `00D734E7` / `0016700C` | 56 / 39 / 46 | 14 / 11 / 14 / 1 | `d5413273f69b018b66fcd6fe31bfa7be15b338cb6cb8fd17d83f7e14c4e4be82` |

The accepted QFU does not expose a defensible mission QL in these five
captures. The emitted bundles therefore document QL as unresolved. Allowing
these captured mission families at QL 1 through 250 is an explicit Rebirth
selection policy, not a captured AO fact.

## Objective correlation

An objective is selectable only when exactly one physical record correlates to
an exact identity field in the accepted QFU. The extractor does not select an
item or NPC merely because it appears in the same playfield.

| Capture | Accepted-QFU evidence | Correlated physical record | Result |
|---|---|---|---|
| `20260728-001044` | `QuestActions[0].UnknownId2 = C350:79A16B61` | SCFU `C350:79A16B61`, Pedro Peasley | Kill Person NPC objective |
| `20260728-003410` | `QuestActions[0].Action = C74A:2586CCB1` | WeaponItemFullUpdate `C74A:2586CCB1` | Return Item objective |
| `20260728-005042` | `QuestActions[0].Action = C73D:57AC07B0` | SimpleItemFullUpdate `C73D:57AC07B0` | Find Item objective |
| `20260728-010220` | `QuestActions[0].UnknownId1 = C73D:57A3C596` | SimpleItemFullUpdate `C73D:57A3C596` | Repair objective |
| `20260728-012547` | `QuestActions[0].UnknownId2 = C350:79A16EB9` | SCFU `C350:79A16EB9`, Emery Ratti | Find Person NPC objective |

For Repair, `C73D:57A3C596` is the QFU-correlated objective.
`C73D:57A3C597` is a separate observation and is not promoted.

Owned item-family packets that have no physical placement are preserved with
full raw provenance as
`non_layout_owned_item_inventory_or_lifecycle_observation`. They are excluded
from `layoutSlots.objectives`. A positional item with an encoded unexpected PF2
is a blocking error.

## Exit-boundary rule

Exit selection uses decoded wire fields and placement, not a guessed topology
grammar:

1. Normalize DoorFullUpdate observations by captured identity.
2. Require a fully decoded door with `Unknown6 = 2`,
   `Unknown7 = 0xFFFF0000`, and an empty undecoded tail.
3. Require that sentinel candidate to be unique.
4. Require it to be the unique sentinel door nearest the C79F interior spawn.

| Capture | Exit identity | Distance from interior spawn | Post-layout exit teleport |
|---|---|---:|---|
| `20260728-001044` | `C748:109AAC07` | `1.80007958` | not captured |
| `20260728-003410` | `C748:109AD151` | `1.80007958` | captured |
| `20260728-005042` | `C748:109AC391` | `1.800081` | captured |
| `20260728-010220` | `C748:109AB591` | `1.80007958` | captured |
| `20260728-012547` | `C748:109AACF8` | `1.80007386` | captured |

The Kill capture remains selectable because its decoded, unique boundary door
is complete evidence; the absent post-layout teleport is retained as
`exit_teleport_missing`, not fabricated.

## Canonical extraction and rejection rules

The canonical artifact preserves:

- accepted QFU type, icon, building, terminal, exterior entrance, mission key,
  action identities, scalar fields, and raw provenance;
- entry teleport, exact C79F packet fields, exact generator payload bytes and
  hash, building, PF2, and interior spawn;
- exact DoorFullUpdate stats and trailing fields;
- positioned chest/item stats and supported trailing fields;
- VendingMachineFullUpdate type, placement, PF2, stats, display string,
  optional identities, and trailing fields;
- SCFU level, health, health damage, monster data/scale, head mesh, textures,
  meshes, decode consumption, and tail;
- CharInPlay and mission lifecycle packets with full raw bytes;
- raw observation arrays, normalized physical layout slots, identity mappings,
  issues, completeness, and selectability.

Selection evidence fails closed when:

- required evidence lacks raw provenance or
  `PreservationStatus=raw_complete`;
- required evidence comes from another capture session;
- the PAF, teleport, QFU, payload building, or physical slot PF2/building
  correlations disagree;
- a critical record cannot be decoded;
- an NPC SCFU is not fully consumed or retains an undecoded tail;
- duplicate immutable records conflict;
- an NPC/objective identity overlay disagrees on PF2, parent, placement,
  heading, template/name, or decoded SCFU fields;
- the objective or exit correlation is missing, ambiguous, or conflicting;
- a float is non-finite; or
- the captured PF2 is the explicitly incomplete shape `1441804`.

Identity-only chest observations do not claim a captured PF2:
`capturedPf2` and `capturedPf2Hex` are `null`, their position is `null`, and
they are excluded from physical chest slots. An actually encoded off-PF
positional chest remains a blocking error.

## Full capture-corpus result

The full captures directory was analyzed in one pass:

```text
ACG corpus analyzed=97 extractionFailures=0 selectable=5
```

The canonical corpus contains:

- 97 capture artifacts;
- 0 extraction failures;
- 5 artifacts containing a generator C79F PAF;
- exactly those 5 artifacts at `complete_and_selectable`;
- 92 non-ACG or incomplete capture directories at
  `incomplete_and_non_selectable`; and
- no other selectable capture.

Final warnings are evidence annotations, not errors:

| Capture | Warnings |
|---|---|
| `20260728-001044` | `exit_teleport_missing`; two `chest_non_layout_identity_only_observation` |
| `20260728-003410` | one `chest_non_layout_identity_only_observation` |
| `20260728-005042` | none |
| `20260728-010220` | none |
| `20260728-012547` | none |

All five have zero extraction errors.

## Runtime catalog: generated five, legacy eight, and 1441804

`MissionAcgLegacyLayoutCatalogFactory.Create()` combines the generated captured
bundles with eight older structural layout families:

1. `1441800`
2. `1443840`
3. `1460226`
4. `1456133`
5. `1419310`
6. `1419335`
7. `1419382`
8. `1419349`

The eight legacy bundles retain generator payload, entry, door, chest, and
structured NPC shape evidence. They remain
`StructurallyCompleteObjectiveIncomplete` and are not selectable because they
lack the exact exit/objective/lifecycle correlation required by this contract.

PF2 `1441804` remains an explicit exclusion, not a bundle. It has partial NPC
shape evidence but no coherent matching generator, door, chest, objective,
exit, accepted-QFU, and teleport lifecycle set. No other payload or layout may
be substituted for it.

With the generated catalog present, the catalog therefore contains five
capture-backed selectable bundles plus eight audit-visible nonselectable legacy
bundles, and one explicit `1441804` exclusion.

## Model, selector, and binding boundaries

These three layers have different responsibilities:

- `MissionAcgLayoutBundle` is the immutable evidence model. It carries the
  exact captured payload, layout slots, objective/exit evidence, provenance,
  compatibility, and completeness.
- `MissionAcgLayoutSelector` is a deterministic Rebirth selection policy. It
  filters to catalog-admitted `CompleteSelectable` bundles compatible with
  mission type and policy QL, then derives a stable choice from the seed,
  mission inputs, and owner/team identity. It is not the official AO layout
  RNG.
- `MissionAcgInstanceBinding` is an immutable descriptor for a future accepted
  mission instance: accepted quest, owner/team, type, QL, mission key, exterior
  entrance, selected bundle, ACG building, allocated live PF2, expiry, and
  optional seed. The class does not itself persist, allocate, enter, spawn, or
  clean up an instance.

The generated `.g.cs` catalog is deterministic source material. It does not
randomly generate a new room graph and does not by itself make mission entry
operational.

## Validation commands and observed output

Analyzer build:

```text
cmd /d /c MSBuild.exe tools-temp\AOSharpMissionCaptureAnalyzer\AOSharpMissionCaptureAnalyzer.csproj /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal
PASS
```

Analyzer self-test:

```text
cmd /d /c tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe --self-test
AOSharp mission capture analyzer self-test PASS
```

The self-test covers the exact five building/PF2/hash/raw-count/slot-count
tuples, exact objective identity and QFU field including Repair `.596`, exit
sentinels, canonical determinism, and negative cases for building/PF2 mismatch,
off-PF physical records, cross-session mixing, missing provenance,
non-`raw_complete` evidence, missing payload, non-finite floats, and incomplete
NPC decoding.

Full corpus:

```text
cmd /d /c tools-temp\AOSharpMissionCaptureAnalyzer\bin\Debug\AOSharpMissionCaptureAnalyzer.exe --corpus tools-temp\AOSharpLiveCapture\bin\Debug\captures
ACG corpus analyzed=97 extractionFailures=0 selectable=5
```

Kill capture extraction and replay:

```text
20260728-001044: ACG extraction status=complete_and_selectable selectable=True
20260728-001044: mission replay decoded=1974 rawPreserved=505 skipped=1990 errors=0
```

## C79F evidence boundary

The extractor knows the exact C79F wire bytes, captured building identity,
captured PF2, interior spawn, payload bytes, payload-leading building identity,
and payload SHA-256 for each finalized capture.

Fields still named `Unknown*` and the remaining payload bytes remain opaque.
The implementation does not assign them room, corridor, door, population,
probability, or seed semantics. The payload is retained as immutable captured
evidence. Reproducing it is different from understanding or implementing AO's
procedural-layout algorithm.

## Deferred production stage

The following work is intentionally not completed by this evidence/catalog
stage:

- persist one selected bundle against an accepted mission;
- allocate and isolate a unique live mission PF2;
- wire exterior entry, teleport, PAF, and re-entry to that binding;
- instantiate doors, chests, NPCs, terminals, and objective objects from the
  selected bundle with safe live identities;
- derive or implement collision and room/tile resource loading;
- preserve per-instance door, chest, NPC, and objective state;
- implement all five objective completion paths and rewards;
- prevent cross-mission and cross-team state leakage;
- expire and clean up completed or abandoned instances; and
- perform private-server lifecycle regression capture after runtime wiring.

Any future database-schema change still requires separate explicit approval.
