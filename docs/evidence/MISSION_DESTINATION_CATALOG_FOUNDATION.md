# Mission destination catalog foundation

## Outcome and evidence boundary

This foundation provides compact content, immutable placement records and exact
lookup operations. It contains no destination selector or engine integration.

| Population | Meaning |
| --- | --- |
| **2,242** | Complete extracted client ACGEntrance placement universe in this evidence version |
| **812** | Distinct placements observed in raw-backed exact random-mission captures |
| **92,830** | Exact destination observations supporting those 812 identities |
| **1,430** | Client placements not yet observed in this capture corpus |
| **2,678** | Five-offer cohorts containing legitimate repeated destination selections |

**PROVEN:** the audited placements have unique complete identities and zero
same-playfield exact-coordinate collisions. **OBSERVED:** the exact capture
population is Omni and records expected mission QLs, character levels, mission
types and terminal geography. These observations do not establish restrictions,
selection weights, probabilities or complete eligibility.

The 812 identities are not the number of offers and are not a complete proven
eligible destination pool. The 2,242 client placements are not a proven mission
destination pool. The remaining 1,430 records are retained as
`CLIENT_ACGENTRANCE_NOT_YET_OBSERVED_IN_RANDOM_MISSION_CAPTURE`, never ineligible,
unused, invalid or non-mission.

The 355 historical missing-raw offers contribute no observed identities or
observation metadata to this catalog. Their diagnostic candidates remain in
the unchanged research artifacts. All operational entrance keys and effective
stat-BD values remain explicitly null. Scale, physical-door identity and
room/building/statel relationships are not invented.

## Exact source and ownership

Starting evidence commit:
`a259df4b118ee6074dd17c1da91d650d95fb5ea1`.

Branch: `codex/mission-destination-catalog-foundation`, created separately from
that exact SHA. This is a foundation handoff from the accepted research corpus,
not a statement that the old research branch is current public-master runtime.
The completion commit is recorded in Git and the task handoff rather than
embedded as its own hash.

The following reports were read completely:

- [ACGEntrance reconstruction](ACGENTRANCE_REGISTRY_AND_MISSION_DESTINATION_RECONSTRUCTION.md).
- [Observed destination eligibility](MISSION_DESTINATION_ELIGIBILITY_FROM_RESOLVED_CAPTURE_CORPUS.md).
- [Duplicate and integrity audit](MISSION_DESTINATION_DUPLICATE_AUDIT.md).

The generator verifies all 53 generated artifacts governed by those reports'
manifests. Its provenance inventory contains 60 repository files: those 53
outputs, three manifests, three reports and the extraction source manifest.
The manifests/reports are pinned to exact Git blobs at the starting commit;
their governed output SHA-256 values must match. Inputs are rehashed before a
generation is accepted. Changed evidence cannot silently retain the original
evidence commit label.

No historical evidence, reconstruction output, eligibility output or
duplicate-audit output is rewritten. No private GameData or original capture
store is copied into the content. Generation reads repository evidence only;
normal catalog loading reads three compact content files only.

## Existing architecture and placement

The shared loader is in
`AORebirth/Libraries/Source/Utility/GameData/Missions/`. Inspection showed that
Utility is an existing shared configuration/helper layer with no DAO, MySQL or
Dapper dependency. Core and PlayfieldLoader have database dependencies and are
unsuitable for this neutral component. The new source itself uses only BCL
collections, serialization, file access and hashing.

The existing `Utility.csproj` includes the three source files and the BCL
`System.Runtime.Serialization` reference. Models use C# 7.3-compatible immutable
properties and cloned read-only collections. The isolated test executable
compiles the same source files without the broader Utility/engine graph,
following the repository's existing source-linked .NET Framework 4.8 test
pattern.

Generated content is under the established current-product content location:

```text
AORebirth/GameData/Missions/Destinations/
    MissionEntrancePlacements.json
    ObservedMissionDestinations.json
    MissionDestinationCatalogManifest.json
```

That GameData subtree did not yet exist at the required evidence base. Creating
this content directory follows the current product layout; it does not add a
new content-selection or staging system. The three files total approximately
2.85 MiB. The 93,185-row research inventory is not shipped in this catalog.

## Content contract

`MissionEntrancePlacements.json` contains 2,242 placement rows. Each preserves:

- complete unsigned `IdentityType` and `IdentityInstance`;
- independently serialized `PlayfieldId`;
- local XYZ values and all three original binary32 bit patterns;
- four raw binary32 rotation components without conversion or normalization;
- exact display name, raw name bytes and name encoding/source provenance;
- referenced template instance and placement-container resource identity;
- placement record offset, length, SHA-256 and source database SHA-256;
- `CLIENT_ACGENTRANCE_PLACEMENT` plus the explicit observation classification;
- null `OperationalEntranceKey` and `EffectiveStatBd`.

Names are descriptive metadata, not keys. The large `a building` family retains
188 separate identities. PF640 identity `0xC0000280` retains the byte-preserving
name `Ænima HQ` and raw bytes `c66e696d61204851`. The six PF100 template-named
`ACG Entrance` placements remain present with their referenced-template name
provenance.

`ObservedMissionDestinations.json` contains 812 identity-keyed evidence rows.
It aggregates only the raw-backed exact audit population and checks the full
offer provenance join against the eligibility inventory. It preserves
observation, request, cohort and session counts, plus observed expected mission
QLs, character levels, mission types, terminal playfields and faction labels/raw
values. Aggregates are reconciled with the existing destination-count artifact.

The QL property is `ObservedExpectedMissionQls`. It is not live-returned mission
QL; that response-side field remains unproven. Mission-type associations are
observations, not exclusions. `ObservedFactionSides: ["Omni"]` and raw side
value 2 are paired with `OBSERVED_WITH_OMNI`, not Omni-only. Terminal playfields
are provenance/geography; no physical terminal owns a destination pool here.

Both files have schema version 1 and distinct catalog kinds. Rows sort by
complete identity. Metadata sets sort deterministically. JSON uses compact
sorted keys, ASCII escapes and a final LF. The manifest records both content
hashes and row counts, the evidence SHA, all repository-relative input hashes,
the generator hash, corpus counts and explicit evidence boundaries. No active
worktree path is embedded.

## Loader and validation contract

```csharp
using Utility.GameData.Missions;

var catalog = MissionDestinationCatalog.Load(selectedGameDataRoot);
var placement = catalog.GetByIdentity(identityType, identityInstance);
var evidence = catalog.GetObservedEvidence(placement.Identity);
```

`selectedGameDataRoot` is supplied by the caller's existing configuration owner.
The loader does not search for captures or choose between private/public data
roots. The optional `repositoryRoot` argument adds development validation of
the manifest's source and generator hashes; it is not required for runtime
lookup.

| Operation | Contract |
| --- | --- |
| `GetByIdentity(type, instance)` | Exact complete identity; missing identities throw |
| `TryGetByIdentity(type, instance, out placement)` | Exact identity lookup without fallback |
| `TryGetByExactWorldPos(playfield, xBits, yBits, zBits, out placement)` | Exact playfield plus binary32 bit key |
| `GetPlacementsForPlayfield(playfield)` | Read-only placement list, empty when absent |
| `IsObservedRandomMissionDestination(identity)` | Membership in the observed evidence set |
| `GetObservedEvidence(identity)` | Read-only metadata; null for an unobserved identity |

The loader validates the schema/kinds, accepted version-1 counts, both payload
hashes, unique complete identities, unique exact coordinate keys, positive
playfields, finite exactly representable binary32 coordinates/rotations and
coordinate-value/bit agreement. It rejects duplicate placement/observed rows,
contradictory classifications, missing observed placement references, incorrect
observed playfield/observation totals, incoherent counts, invalid metadata
sets, conflicting name bytes and fabricated non-null operational fields.
Required provenance paths are repository-relative. Malformed data is rejected;
duplicates are never silently discarded and ambiguous coordinates never choose
the first row.

Coordinate lookup uses neither tolerance, rounding, nearest-neighbor matching
nor name fallback. Cross-playfield identical transforms remain different
placements. Public placement records, nested provenance and observed metadata
collections expose no mutation API.

## Placement uniqueness and repeated selection

```text
DESTINATION_UNIQUENESS_WITHIN_COHORT_REQUIRED=NO
```

Unique catalog identities prevent accidental duplicate content rows. They do
not require distinct destination identities in a roll result. The duplicate
audit proves 2,678 legitimate five-offer cohorts with repeated exact identities,
covering 2,887 repeated offer positions with distinct raw byte ranges. A future
selection result may reference the same immutable placement more than once.
This library contains no roll-result deduplication helper, random selection,
weight calculation or eligibility filter.

## FUTURE runtime-owner handoff

These flows are documentation only; none is wired in this task:

```text
FUTURE
Mission offer generator
    | obtains selected placement identity through separately accepted selection rules
    v
MissionDestinationCatalog
    | returns the exact placement record
    v
Quest / WorldPos construction
```

```text
FUTURE
selected entrance identity
    -> accepted mission persistence
    -> outdoor entrance binding
    -> mission instance allocation
```

The complete placement identity is not an operational entrance key. The runtime
owner must separately establish persistence, outdoor binding, instance
allocation and any required door/room relationship before activating those
flows. Observed QL/type/faction/terminal associations must not become invented
selection restrictions or probabilities.

Current master already provides
`AORebirth/Libraries/Source/AORebirth.Core/GameData/GameDataPaths.cs`; a future
caller should pass its selected root, preserving existing
`AO_REBIRTH_GAMEDATA_PATH` behavior. This selector is not reimplemented here.

The required evidence SHA predates current `.NET 10` build infrastructure.
During future public-master integration, the runtime owner should include the
new Utility sources in the existing explicit source inventories:

- `WindowsBuildNet10/Projects/Utility.ZoneNew.WinNet10.csproj`;
- `SharedBuild/source-inventory/Utility.CompileItems.props`, consumed by the
  existing Utility engine build project.

Those absent trees are not imported into this evidence branch. The future
owner also needs ordinary content packaging of the three catalog files into
the selected GameData tree and compilation on the then-current public master.
This handoff does not claim production or .NET 10 integration acceptance.

## Reproduction and validation

From this branch's repository root:

```text
cmd /d /c Tools\mission_destination_catalog.cmd generate
cmd /d /c Tools\mission_destination_catalog.cmd test
cmd /d /c Tools\mission_destination_catalog.cmd generate --check
git diff --check
```

`test` runs the deterministic Python evidence/content checks and builds/runs
the source-linked C# loader tests. It does not build, start or stop engines.
Normal catalog loading is also exercised from a temporary directory containing
only the three content files, without repository evidence.

All requested research regression tests and checks passed in the unchanged
starting-evidence checkout. The historical duplicate-audit command includes a
Git change-scope guard specific to its original audit task; its immutable
checkout is used rather than changing that guard or its outputs to accept a
later feature branch. The foundation generator verifies the same historical
artifact bytes, and the final change review verifies that historical tooling,
evidence and outputs are untouched.

```text
cmd /d /c Tools\acgentrance_reconstruction.cmd test
cmd /d /c Tools\acgentrance_reconstruction.cmd generate --check
cmd /d /c Tools\mission_destination_eligibility_analysis.cmd test
cmd /d /c Tools\mission_destination_eligibility_analysis.cmd generate --check
cmd /d /c Tools\mission_destination_duplicate_audit.cmd test
cmd /d /c Tools\mission_destination_duplicate_audit.cmd generate --check
```

The duplicate regression uses the same canonical private July input selection
as its existing manifest. Those private inputs are not consumed by the new
catalog generator or loader.

New validation covers exact counts and membership, known PF505 anchors, exact
identity/bit lookups and wrong-bit misses, cross-playfield transforms, repeated
names and raw encoding, complete metadata aggregation, retained unobserved
placements, immutable collections, malformed/duplicate/collision fixtures,
hash and evidence tampering, missing-raw exclusion, deterministic generation
and stale artifacts. JSON validation also rejects a second trailing document,
incomplete roots and contradictory duplicate members while accepting legal
whitespace and escaped delimiters inside strings.

Final validation results:

| Validation | Result |
| --- | --- |
| Catalog generation | PASS; 2,242 placements, 812 observed identities, 22 observed playfields |
| Python catalog checks | PASS; 29 tests |
| Source-linked C# loader build/checks | PASS; clean compile, 47 tests |
| Catalog deterministic `generate --check` | PASS |
| Reconstruction tests / check at unchanged evidence SHA | PASS; 17 tests and unchanged artifacts |
| Eligibility tests / check at unchanged evidence SHA | PASS; 7 tests and unchanged artifacts |
| Duplicate audit tests / check at unchanged evidence SHA | PASS; 50 tests and unchanged artifacts |
| Historical tools, evidence and both engine trees | Unchanged |
| `git diff --check` | PASS |

The build result applies to the isolated loader source, not a production engine
build or future .NET 10 integration. No client, capture, database or service was
started or changed.

Inspected files include the three governing reports and their manifest-governed
outputs, placement and offer evidence, existing Utility/Core/PlayfieldLoader
project dependencies, current GameData selection/layout and the existing
source-linked test pattern. Changed files are limited to the three generated
content files, the Utility catalog source/project entries, the new generator
and tests, and this handoff report. No engine, DAO, database schema or historical
mission evidence file is changed.
