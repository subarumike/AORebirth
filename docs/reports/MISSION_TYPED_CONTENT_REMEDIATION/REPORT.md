# Mission typed-content remediation — Windows candidate

Status: implementation and component verification complete; **acceptance blocked**.

Follow-up: [connected Windows acceptance](CONNECTED_FOLLOWUP.md) records the
Docker repair, connected matrix PASS and remaining acceptance limits. The
original results below are retained as history.
Base: public `master` at `3c904f760513af8110b78e353c09a9296a0d76a9`.
Branch: `codex/mission-typed-content`. No commit, push, deployment, evidence cleanup,
private GameData mutation, or change to GameData root selection was performed.

## Root cause and scope

PROVEN by the audited code paths: permanent mission JSON still embedded captured
response bodies and spawn packets, and runtime services decoded/reconstructed
those bytes while composing offers, materializing objects/NPCs and projecting
corpses. Eligibility validation additionally inspected capture packet presence,
identity offsets and hashes. Normalized metadata alone had not severed that
runtime dependency. This candidate addresses only those five audited paths.

| Old consumer | New permanent content and consumer |
|---|---|
| `GeneratedMissionWire.CapturedBody` / `MissionRollCaptureLibrary` / `MissionRollCaptureTemplate`, called by `GeneratedMissionRollService` | `MissionOfferContent` reads `MissionOffers.json`; typed `QuestAlternativeMessage` and `QuestInfo` copies; ordinary existing AOtomation serializers emit responses. The two raw-input accessor classes are removed. |
| `MissionAcgLayoutCatalogLoader` packet presence, offset, identity, packet-hash and PF1441804 capture gate | Format-2 typed layouts; generic completeness, compatibility, geometry, placement identity, source-playfield, category and typed-content validation. Generator-payload integrity remains. |
| `GeneratedMissionNpcFactory` via `GeneratedMissionNpcEvidence.CopySpawnMessage` historical SCFU decode | `MissionNpcAppearance` consumed directly by the factory, preserving scale, head mesh, texture and mesh layers, including weapon-selection inputs. |
| `MissionAcgRuntimeMaterializer` packet retargeting and `MissionStaticDynel` historical decode | `MissionSpawnContent` typed Door/Chest/Item/Weapon definitions. Normal serializers receive a copied definition with the live identity/playfield. No packet scanning, offset patching or historical decoder. |
| `GeneratedMissionCorpseProjection` historical NPC SCFU decode | The same separate per-instance typed appearance copy used for the living NPC. Existing corpse serialization behavior remains. |

## Every normalized field and content preservation

`normalized-fields.json` is the exhaustive machine-readable field inventory:
226 promoted field paths, source hashes, decoder hash, and 238 decoded record
representations. It also lists decoder-visible fields separately; that list is
not a claim that discarded SCFU transient state is now runtime content.

- 13 response bodies / 65 offers: every decoded response, offer, action, reward,
  identity, text, version, slider, coordinate and measured unknown field is
  retained as typed JSON. Empty arrays are retained. All 13 serialize exactly
  to their original historical bodies. Existing policy-driven reward, location,
  text, difficulty and identity substitutions remain unchanged.
- NPC appearance: `MonsterScale -> Appearance.Scale`, `HeadMesh -> HeadMesh`,
  `Textures[].Place/Id/Unknown`, and
  `Meshes[].Position/Id/Layer/OverrideTextureId`. This covers 79 ambient slots
  plus two NPC objective aliases. Null mesh/texture arrays become empty arrays,
  matching the previous factory's explicit empty-array behavior; no unknown
  appearance values or gameplay defaults were invented.
- Static content: every property of the existing DoorFullUpdate,
  ChestItemFullUpdate, SimpleItemFullUpdate and WeaponItemFullUpdate typed
  models, including transform, ownership, state-machine identity, stats,
  auxiliary identities, versions and measured unknown fields. 157 static
  representations (including exit/objective aliases) reproduce their complete
  original packets byte-for-byte before live identity projection.
- The ownerless return-item WeaponItemFullUpdate codec lacked its world
  transform. The measured packet required position and quaternion between owner
  and playfield. Conditional fields now follow the existing ownerless-item
  serializer pattern; owned/equipped weapons retain their previous format.
- All five bundles retain generator payloads, geometry, entry/exit placements,
  149 dynels, 79 NPC slots, five objectives, five exits, roles, names, MonsterData,
  templates, original slot IDs, compatibility, completeness and provenance.
  Existing `RewardObservations.json`, `RollPolicy.json`, rewards, locations,
  text, generation settings, combat policy, rare loot, NPC templates and other
  playfield content are unchanged.
- All payload representations are removed recursively from the normalized
  layouts: `RawPacketHex`, `PacketHex`, `rawPacketHex`, `rawPacket`, `packetBytes`
  and retarget-slot aliases. Guards are case insensitive. Historical packet
  hashes/lengths inside provenance remain inert metadata, not eligibility inputs.
- Public historical `RollBodies.json` and `RollTemplate.json` remain retained,
  excluded from NewEngine packaging. The original public layout JSON is
  preserved at `Tests/Fixtures/Gameplay/Missions/HistoricalLayouts.json`.
  No canonical capture collection or private source dataset was modified.

## Durable compatibility

`durable-identities.json` records all five unchanged bundle IDs and hashes.
Canonical bundle identity remains SHA-256 of the generator payload, with the
existing lowercase DAO representation. Format version changes to 2; the payload,
ID, hash, slot ordering and runtime identity allocation algorithm do not change.
No mission-binding migration or blind hash rewrite is needed or performed.
The untouched private Missions inputs are semantically identical to the retained
public historical inputs, but still use layout format 1. A new runtime pointed
at that unchanged private root will fail closed until normalized content is
promoted there separately; this task validates only the isolated copy.

OBSERVED: a localhost-only, read-only query of the approved local database found
zero persisted generated mission bindings. Production databases were not queried.
PROVEN in component tests: old bundle identity matches the preserved source;
accepted offer snapshots, damaged/moved/dead NPCs, corpses and mission restoration
retain their durable contracts. This is not proof of a production DB corpus.

## Validation and exact limits

| Validation | Result |
|---|---|
| Approved Windows engine build | PASS |
| Offline normalization and complete offer/static byte equivalence | PASS |
| Repeated offline export vs checked-in candidate and isolated GameData | Byte-identical |
| Isolated full NewEngine suite | 867 passed, 2 failed, 869 total; all 102 mission-named tests passed |
| Unchanged public baseline full NewEngine suite | 861 passed, same 2 failed, 863 total |
| AOtomation complete suite | 632 passed, 2 failed, 634 total |
| Unchanged public baseline AOtomation suite | Same 632 passed and same 2 failed |
| Connected fixture compilation | PASS |
| Secret scan / whitespace validation | PASS |
| Full ZoneEngine_New startup and connected mission lifecycle | BLOCKED before startup by Docker Desktop initialization |
| Commit/push gate | NOT PASSED; no commit or push |

NewEngine failures reproduced unchanged on public master:
`BuildingExitProxyTests.Pf2064_return_exit_matches_the_legacy_statel_collision_envelope`
and `NpcNavigationTests.Pf127_AbmouthReachesThePlayerStandingOnTheFloorAbove`.
AOtomation failures reproduced unchanged: stale NpcTemplateCatalog input hash in
`CurrentConsumerAuditSeparatesHistoricalEvidenceFromRuntimeActivation`, and the
missing opt-in config element expected by `CellHeatSchedulingIsFailSafeAndExplicitlyOptIn`.
No unrelated production changes or test expectation relaxations were made.
An earlier checkpoint timing failure did not recur in final or baseline runs.
The suites also report the existing missing MissionLevels.csv deployment item.

The isolated full GameData copy omits RollBodies, RollTemplate and Provenance,
and uses the normalized packet-free layouts/offers. The suite covers typed rolls,
acceptance projections, all five mission-world materializations, NPC creation,
appearance, weapons, static serialization, durable death/corpse projection and
accepted mission restoration. These are component/service tests; they do not
establish full live-process login, roll, accept, enter, combat and reconnect.
The existing connected fixture seeds an accepted mission and tests reconnect;
additional connected roll/accept/entry/death coverage remains required for the
full user-requested acceptance matrix even once Docker is repaired.

Docker failed before fixture/database/engine startup:
`initializing Inference manager ... dockerInference: The file cannot be accessed
by the system (listener: The filename, directory name, or volume label syntax
is incorrect.)`. No Docker reset, reinstall or machine configuration repair was
attempted as part of this mission-only change. No game client was launched.

A managed C-drive worktree backed by D-drive Git metadata triggers the existing
AOtomation generated-artifact same-volume guard. The unchanged wrapper was run in
an independent C-drive validation clone of the exact public base plus candidate
files, checked for byte equality. No guard/lease bypass or build-system change.
The mandatory gate additionally requires a clean worktree; it cannot be claimed
passed while this candidate remains uncommitted under the user's validation gate.

## Remaining runtime relationships and work

No historical payload consumer remains in the five scoped paths. Legitimate
capture-derived permanent data remains: typed mission content, reward/policy
content, NPC combat configuration and ACG generator bytes. Provenance paths are
metadata and are never opened by these consumers. `GeneratedMissionWire.Read`
remains for current server-created `FrozenWireBody` DAO snapshots, not the
historical response corpus. The pre-existing reusable `Corpse.json.TemplateHex`
serializer template is retained; only its historical NPC input was in this scope.
The retired engine/tools/tests may still read their historical fixtures; they
are not imported or executed by NewEngine production startup.

Remaining acceptance work: restore the disposable Docker fixture, finish the
full connected mission action matrix, resolve or separately authorize treatment
of the four baseline suite failures, then rerun the required acceptance gates.
Do not commit/push or deploy this candidate before those gates pass.

Changed-file inventory: `files-changed.txt`. Detailed mission test results:
`validation.json`. Local build and TRX logs remain under ignored `build-verify`.
