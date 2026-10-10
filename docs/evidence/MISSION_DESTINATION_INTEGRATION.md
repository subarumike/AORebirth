# Mission destination integration

Date: 2026-10-09

Branch: `codex/mission-destination-integration`

Start: public master `bddea56ca2d47c5eb64ed53f6e9efd279ff4f694`.
The implementation commit containing this report identifies the resulting source.

## Outcome and boundary

PROVEN by source review and the focused checks below: the active path is
`MissionDestinationCatalog -> GeneratedMissionRollService -> frozen offer ->
QuestDungeonService -> existing quest/key/dungeon/entry systems`.
There is one entrance/destination data owner. No provider, facade, repository,
or resolver service was introduced.

The catalog preserves 2,242 unique physical ACGEntrance placements. Exactly
812 have retained observed-retail metadata from 92,830 exact raw-backed offers.
The remaining 1,430 are unobserved, not proven ineligible. The 355 offers whose
raw evidence is missing are excluded from promotion. This evidence population
is Omni; it does not establish that these entrances are Omni-only.

UNVERIFIED: live AO client acceptance, retail destination probabilities, and
uncaptured QLs/conditions. No objective, Kill Person fencing, reward,
completion transaction, operational BD/key reconstruction, Linux, or deployment
work was performed. Existing objective/reward behavior is not certified by this
destination slice.

## Evidence and data ownership

The three required reports were read completely before edits:

- `docs/evidence/MISSION_DESTINATION_CATALOG_FOUNDATION.md` at foundation
  `f07bb3c1a99218433e7d459c95ba29ccb22c36b2`.
- `docs/evidence/MISSION_DESTINATION_DUPLICATE_AUDIT.md` and
  `docs/evidence/ZONEENGINE_NEW_MISSION_CURRENT_STATE_AUDIT.md` from audit
  `42046ddbd19d5592f5183d5d398aba4f29606115`.

The foundation's three JSON files are imported byte-for-byte. The shared
catalog's three existing source files are imported and extended directly.
`MissionDestinationSelection.json` adds captured joint condition membership and
WorldPos offsets; it does not replace or alter the physical placement evidence.

The offline generator reads the complete pinned public Git evidence blobs at
the foundation commit, joining request/offer provenance across:

- `docs/generated/missions/destination-eligibility-analysis/mission-offer-analysis-inventory.jsonl.gz`
- `docs/generated/missions/destination-duplicate-audit/offer-audit-inventory.jsonl.gz`
- `docs/generated/missions/destination-duplicate-audit/request-audit-inventory.jsonl.gz`

It validates every promoted raw WorldPos against full placement identity,
playfield and XYZ bits, rejects conflicting offsets, and records input,
generator and canonical payload hashes. The output has 180 complete request
conditions, 547 condition/type sets, 25,296 identity memberships and 812 exact
WorldPos records. Every available promoted observation is used; the excluded
355 do not have the required raw evidence. No observation count becomes a
selection probability.

Runtime reads only these four files from the existing configured GameData root:

- `Missions/Destinations/MissionEntrancePlacements.json`
- `Missions/Destinations/ObservedMissionDestinations.json`
- `Missions/Destinations/MissionDestinationCatalogManifest.json`
- `Missions/Destinations/MissionDestinationSelection.json`

Runtime startup requires selection data. It does not read Git, research folders,
packet corpora or capture directories. Existing `AO_REBIRTH_GAMEDATA_PATH`
selection and GameData packaging remain in use. No private GameData is imported
into this public change.

## Field trace and runtime behavior

| Retired input or responsibility | Current source or behavior |
| --- | --- |
| `MissionLocationPool.Spots` and `MissionRollLocations` | Removed; exact captured condition membership in the shared catalog supplies destinations. |
| Rounded PF/XYZ from the 140-row population | Selected placement's playfield and exact local float bits. No fuzzy conversion of old rows. |
| `EntranceLow/High`, later `BuildingLowId/HighId` | Captured WorldPos offsets X/Z; projected through existing wire fields `Unknown18/19` and existing persistence fields unchanged. |
| Entrance identity found at acceptance by one-meter X/Z proximity | Complete type plus instance selected during generation, frozen with the offer and resolved exactly at acceptance. |
| `MissionEntranceCatalog` and `MissionEntrances.json` | Deleted; dungeon entry, restoration, exit placement and GM quest command use the shared catalog directly. |
| Entrance name | Catalog display name; names are not keys and repeated names remain distinct identities. |
| Entrance rotation | Catalog raw components WXYZ mapped to engine Quaternion XYZW; all 2,242 identities match the current Dynels reader bit-for-bit. |
| Geography/side helper APIs in `MissionLocationPool` | Retained where existing consumers need them; no destination population remains there. |

Selection requires exact character level, expected QL, difficulty detent,
faction side, breed, profession, terminal playfield, complete terminal identity,
all six raw secondary slider bytes and mission type. There is no nearest-QL,
terminal, faction, slider or type fallback. Marginal evidence is insufficient.
The raw center slider value is retained, not replaced by a normalized profile.

Selection is uniform over distinct observed identities for that full condition,
with replacement for each offer. Cohort destination repeats are permitted. This
is an explicitly non-retail-weighted initial policy. The existing type-mix
selector is preserved: if any type it selects lacks a captured joint condition,
the entire roll fails closed before offer publication or fee charging. This can
reject a roll even where another type for that request has evidence.
The handler's existing five-ID reservation still precedes generation; rejection
can consume reserved identity numbers. It is not a claim of a write-free handler.

The generated packet and frozen offer use the same selected placement. An
index-aligned read-only identity list bridges the existing wire composer to
existing offer projection without changing the packet schema. Acceptance checks
complete entrance identity, destination type/playfield/instance, all three XYZ
bit patterns and both offsets. Coordinates validate the chosen identity; they
never reconstruct it.

Quest parameters retain complete entrance identity. Historical stored
parameters with an implicit MissionEntrance type retain that same fixed type.
The existing carried-key/link checks, expiry, cleanup, 10m horizontal and 14m
vertical use ranges, procedural layout generator and entry/exit mechanisms are
preserved. Rotation conversion changes only representation order; it does not
change door clearances or geometry tolerances.

## Explicit DAO exception

Mike approved only `offer.EntranceInstance <= 0` becoming
`offer.EntranceInstance == 0` in `MySqlMissionDao.Generated.cs`.

Retail entrance instances are UInt32 bit patterns. `0xC00001F9` has a negative
unchanged C# `int` representation. Its sign is not an invalid identity.
The signed SQL INT, serialization, identity type and all 32 bits are unchanged.
No schema change, clamp, remap or conversion of the stored value was made.
Owner, offer, generated quest, key and item positive-only contracts remain.

`DAO_CODE_CHANGED=YES_SCOPED_ENTRANCE_IDENTITY_FIX`

The 74 real disposable-MySQL checks passed before destination integration resumed.
They prove zero rejection, positive acceptance, high-bit offer persistence and
fresh readback, accepted binding readback, and
`unchecked((uint)readBack) == originalUInt32`, plus rejection by the other
positive-only contracts. The existing disposable DAO harness was extended with
`--generated-identity-only`; it did not contact the live database.

## Validation

| Validation | Result |
| --- | --- |
| Clean exact-start approved Windows build | PASS. |
| Clean exact-start full `ZoneEngine_New.Tests` | Compile failure: six pre-existing CS0246 errors; no tests executed. |
| Integrated approved Windows build | PASS, including the final rotation conversion. |
| Integrated default/full test project | Same six CS0246 errors; no new compilation diagnostic. No unrelated test repairs or suppressed default failures. |
| Focused destination suite | PASS, 40/40 after the final rotation conversion; zero failures or skips. |
| Real disposable-MySQL entrance identity checks | PASS, 74 checks. |
| Offline selection regeneration/check | PASS; checked-in output agrees with complete pinned evidence. |
| AO client acceptance | UNVERIFIED; the client was not launched or controlled. |

The six unchanged baseline diagnostics are missing `MissionNpcContent` in
`MissionContentEditabilityTests.cs:154`, `MissionAcgMaterializedInstance` and
`GeneratedMissionWorld` in `GeneratedMissionMaterializationTests.cs:197`,
`MissionAcgLayoutBundle` and `MissionAcgMaterializedInstance` in that file at
line 210, and `GeneratedMissionCorpseDynel` in
`GeneratedMissionCorpseInteractionTests.cs:111`.

The explicit `MissionDestinationFocusedTests=true` property selects only the
new focused tests and shared test doubles. Default/full project compilation is
unchanged. Catalog/generation tests require only the four public compact files.
Dungeon integration and rotation proof additionally require the configured full
GameData root's existing PF324 room/style files and Dynels data. Tests do not
copy those private inputs into the repository.

The dungeon tests exercise real acceptance, QuestService, layout generation,
actual dungeon world construction and entry/restoration code. Their quest store,
network session and inventory boundaries are test doubles. They therefore prove
the service/key/entry flow but are not live-client or real quest-store database
acceptance. Real generated-offer/binding database behavior is covered separately
by the 74-check DAO run.

Commands, from the repository root in a cmd session with the existing
`AO_REBIRTH_GAMEDATA_PATH` pointing to the full GameData root:

```cmd
cmd /d /c tools\build_aorebirth_debug.cmd
dotnet test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj
dotnet test AORebirth\Server\ZoneEngine_New.Tests\ZoneEngine_New.Tests.csproj -p:MissionDestinationFocusedTests=true
cmd /d /c Tools\run_mission_dao_validation.cmd --generated-identity-only
cmd /d /c Tools\mission_destination_selection.cmd generate --check
```

Local ignored validation receipts are under `build-verify/`:
`mission-destination-baseline-build.log`, `mission-destination-baseline-tests.log`,
`mission-destination-final-build.log`, `mission-destination-final-full-tests.log`,
`mission-destination-focused-tests.log` and `mission-entrance-identity-dao.log`.
The focused TRX is in the test project's ignored `TestResults/` directory.

## Files inspected and changed

Inspected: startup authority, project state, code standards and documented
workflows; the three reports above and pinned evidence inventories; existing
catalog/projection/handler/quest/dungeon/entry/rotation code; generated mission
DAO and current schema; existing DAO harness, build inventories, GameData
packaging and relevant tests. No retired engine implementation was searched.

Changed:

- `AORebirth/Libraries/Source/Utility/GameData/Missions/`: three shared catalog
  source files; `AORebirth/GameData/Missions/Destinations/`: four compact JSON files.
- `GeneratedMissionRollService.cs`, `GeneratedMissionRollProjection.cs`,
  `QuestAlternativeMessageHandler.cs` and `Program.cs`: catalog selection,
  identity projection, fail-closed handling and DI.
- `MissionLocationPool.cs`: obsolete population removed;
  `MissionRollLocations.cs`, `MissionEntranceCatalog.cs` and
  `AORebirth/GameData/MissionEntrances.json`: deleted.
- `QuestDungeonService.cs`, `QuestDungeonParameters.cs`,
  `QuestDungeonPlayfield.cs`, `PlayfieldManager.cs`, `QuestsCommand.cs`: direct
  shared-catalog consumption and exact identity flow.
- `MySqlMissionDao.Generated.cs`: the single approved predicate change.
- `WindowsBuildNet10/Projects/Utility.ZoneNew.WinNet10.csproj` and
  `SharedBuild/source-inventory/Utility.CompileItems.props`: catalog source wiring.
- `Tools/mission_destination_selection.py` and `.cmd`: offline evidence export.
- `Tools/MissionDaoValidation/GeneratedEntranceIdentityChecks.cs`, its existing
  `Program.cs`, and `Tools/run_mission_dao_validation.cmd`: focused real-MySQL proof.
- `MissionDestinationIntegrationTests.cs`, `QuestDungeonDestinationTests.cs`,
  and `ZoneEngine_New.Tests.csproj`: focused integration proof and explicit mode.
- `GeneratedMissionRollProjectionTests.cs`, `MissionRollSemanticsTests.cs`,
  `MissionRollTemplateContractTests.cs`: minimal changed-signature wiring only.
- This report, `docs/ai/CURRENT_TASK.md`, `docs/project/PROJECT_STATE.md`.

No production configuration, database schema, live service, private Linux source
or deployment artifact is part of this change.
