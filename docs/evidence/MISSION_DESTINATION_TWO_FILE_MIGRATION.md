# Two-file mission destination catalog

Branch: `codex/mission-destination-integration`. Migration source:
`0721fa8844407bf6e1462341bf46d122bda44437`.

## Runtime data and selection

`MissionDestinationCatalog` loads these two files once from the selected
GameData root's `Missions/Destinations` directory and builds immutable lookup
tables:

| File | Bytes | Contents |
| --- | ---: | --- |
| `MissionDestinations.json` | 657,035 | 47 terminal-playfield/expected-QL pools, 812 distinct identities; their unions cover 45 QLs. |
| `MissionEntrancePlacements.json` | 1,402,358 | 2,242 exact placements; 812 captured WorldPos records and 1,430 explicit null WorldPos records. |
| Total | 2,059,393 | 55.24% smaller than the former 4,601,167-byte four-file set. |

The placement document has schema version 2 and catalog kind
`MISSION_ENTRANCE_PLACEMENTS`. Each row retains the complete unsigned identity,
destination playfield, numeric XYZ plus their binary32 bits, four raw rotation
components, display name and byte-preserving name hex. `WorldPos` stores
`PlayfieldIdentityType`, `WorldOffsetX` and `WorldOffsetZ`, or is null when that
placement has no captured WorldPos. Rotation components retain their original
order and values; no normalization or coordinate conversion is performed.

The destination document has schema version 1 and catalog kind
`MISSION_DESTINATIONS`. Each pool contains `TerminalPlayfieldId`,
`ExpectedMissionQl` and `DestinationIdentities`, whose entries contain
`IdentityType` and `IdentityInstance`. Terminal playfield is the origin; each
referenced placement supplies the destination playfield. The QL-wide fallback
is derived as a distinct union of these origin pools at initialization, avoiding
a second duplicated list in JSON.

The existing level/difficulty calculation still determines expected QL. The
existing mission-type selection, RollMixes, objectives, rewards, text, packet
templates and slider policies are unchanged. Only destination lookup changes:
prefer the originating terminal playfield's exact-QL pool, otherwise use the
same-QL union. An absent QL remains unsupported. Selection still uses the
existing uniform distinct-identity sampling with replacement, allowing repeated
destinations in a five-offer cohort. Capture frequencies are not weights.

No character-level, faction, breed, profession, terminal-instance, secondary
slider or mission-type field restricts the destination lookup. Existing terminal
interaction and side-access checks remain unchanged. Terminal geography and
cross-condition reuse are explicit provisional gameplay policies; these
observations do not prove exclusions or original Funcom probabilities.

## Offline preservation proof

The reproducible command is:

```cmd
cmd /d /c Tools\mission_destination_runtime_migration.cmd generate --check
```

The offline tool reads the immutable four-file baseline and the completed
source-linked offer ledger from that same Git commit. It verifies their hashes,
checks each resolved offer's exact identity, destination playfield, XYZ bits and
WorldPos offsets, then compares original terminal/QL and QL-wide identity sets.
It does not access live captures. Runtime never executes this tool or reads its
research inputs or receipt.

PROVEN: 93,185 unique retained offers reconcile to 92,830 exact resolved offers
and 355 missing-raw unresolved offers. Unresolved rows supply no destination.
All 2,242 placements, 812 observed identities, 812 WorldPos records, 47
terminal/QL pools and 45 QL-wide pools are preserved. Coordinates, rotations,
names, identity bits and offsets match the old data exactly.

| Requested case | Preserved result |
| --- | --- |
| Andromeda PF655, QL29 | 215 destination identities. |
| Borealis PF800, QL29 | 196 different destination identities; combined QL pool 411. |
| Neutral Shade, level25, centered difficulty, Borealis QL25 | Origin/QL pair absent; same-QL fallback contains 124 identities. |
| QL with no pool | No destination fallback, offer publication or fee deduction. |

The migration deliberately uses `original_expected_ql` in the completed ledger
to preserve the working runtime pools. Ten captured level13 labels differ from
the research report's current lookup projection. Regrouping them under the
current projection would remove these three entrances from QL22 (125 to 122):
`56006:0xC00102AD`, `56006:0xC00402AD`, `56006:0xC00802AD`, all PF685. QL23
already contains those observations' identities. This migration does not make
that separate content-policy change or alter the runtime QL calculation.

The machine-readable proof, including all 45 old/new identity-set hashes,
all 47 origin-pool hashes, input/output hashes and the unapplied QL discrepancy,
is `docs/generated/missions/destination-runtime-migration/migration-receipt.json`.
Original conditions and individual offer provenance remain in
`docs/generated/missions/destination-zone-distribution/` and its source corpus.

## Editing and removed dependencies

Future destinations can be added by editing these two JSON files: retain an
exact placement and valid WorldPos, then reference its complete identity from
the intended terminal/QL pools. Restart to reload the catalog. No C# change,
fixed population constant, source SHA or runtime manifest regeneration is
required. A new pool automatically participates in the same-QL fallback union.
The one-time migration generator recreates the historical baseline and must not
be used to overwrite later approved additions.

Structural checks reject malformed or missing files, unsupported schemas,
duplicate placement identities or exact-coordinate keys, numeric/bit
disagreement, invalid names/rotations, duplicate terminal/QL pools, empty pools,
duplicate references, unknown identities and pool members without WorldPos.

The former `ObservedMissionDestinations.json`,
`MissionDestinationSelection.json` and `MissionDestinationCatalogManifest.json`
are retired after the migration and requested regressions pass. Runtime no
longer deserializes capture conditions, observation counts, evidence metadata,
source manifests, source hashes or the 25,296 condition/type associations.
Offline historical generators and reports remain evidence, not runtime inputs.

## Validation and limits

| Validation | Result |
| --- | --- |
| Offline generation and byte-for-byte reproducibility | PASS before and after retirement; all populations and exact QL sets preserved. |
| Approved `tools\build_aorebirth_debug.cmd` | PASS. |
| Focused mission regressions, old files still present | PASS 81/81, zero skips. |
| Real disposable-MySQL entrance identity route | PASS 74 checks, including zero rejection and high-bit `0xC00001F9` readback. |
| Full disposable-MySQL mission DAO regression | PASS 275 checks, including rollback and concurrency. |
| Focused mission regressions after deleting old files | PASS 81/81, zero skips. |
| Exact two-file-only temporary GameData root | PASS; both files independently required. |
| Data-only new placement and QL pool | PASS using test-only data; no runtime count/hash pin. |
| Active runtime source references to three retired filenames | None. |

The 81 checks comprise 41 destination/handler cases, 27 dungeon/lifecycle cases
and 13 existing mission-QL graph cases. They cover all 45 QLs, five-offer
cohorts, destination repeats, both QL29 origins, Shade's retained live-request
metadata, exact frozen destination identity/WorldPos, accepted destination
validation, stored-destination restoration, entry, journal abandonment and
inventory key retirement. Unsupported requests and rejected/throwing
publication preserve cash; real DAO checks separately validate transactional
publication, fees, rollback, key state and unsigned identity preservation.

The three old source JSON files were deleted only after the offline comparison,
build, first 81-test run and both real DAO runs passed. The offline check and
focused suite then passed again with those source files absent. Ignored local
receipts are under `build-verify/mission-two-file-*` and
`build-verify/mission-destination-focused-tests.log`.

The full DAO run used the already-established task-local isolated-source and
`LangVersion=latest` invocation; the default full DAO wrapper has the previously
documented C#7.3 nullable-source compile limitation. The full ZoneEngine_New
test project has the previously documented unrelated baseline compile failures;
the existing `MissionDestinationFocusedTests=true` route was used. Neither
baseline was repaired or represented as a successful full-project suite.
Builds retain NuGet vulnerability advisories and source warnings. One transient
MSBuild content-copy retry in the first focused run recovered successfully.
No production changes were made to silence these diagnostics.

No DAO, schema, dungeon generation, objective, reward, type-mix or QL-calculation
source is changed.

The client previously displayed five offers on `73fc97a75`; that observation is
not a manual acceptance result for this migration. New original-client rolling,
acceptance, entry and deletion remain for Mike to verify. Automated lifecycle
tests use the real mission services with persistence/session doubles; disposable
MySQL checks separately exercise real DAO transactions and identity readback.
Proactive online key dispatch and eviction from an occupied dungeon are not
newly validated by the added lifecycle checks.
No new captures, Linux update, master merge or production deployment is included.

## Files inspected and changed

Inspected the three existing shared-catalog source files, generated-roll service
and projection, roll handler, startup registration, quest/dungeon acceptance and
key paths, destination and dungeon tests, existing validation wrappers, the four
baseline JSON files, and the completed distribution ledger/manifest. Startup and
current workflow/task/project documents established the checkout and commands.

Changed the three catalog source files, the generated-roll lookup, handler
lookup/diagnostics and startup registration; replaced runtime JSON structure and
retired the three obsolete files; updated two test files; added the offline
migration tool/wrapper and receipt; updated this report and current task/project
state notes. Other mission content and persistence/dungeon runtime files remain
unchanged.
