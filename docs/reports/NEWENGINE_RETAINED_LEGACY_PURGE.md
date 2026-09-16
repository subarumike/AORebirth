# Retained Legacy implementation purge

Starting master: `4159a00c0e7c59f3139fa7ce3054d722f001e6a6`.
Branch: `codex/purge-retained-legacy-001`.
Worktree: `C:/Users/Mike/Documents/AORebirth/build-verify/purge-retained-legacy-001`.
Production modified: **NO**. Master and Delmus's branch are unchanged.

## Result and availability

All 57 inventoried relocated files are deleted: nine combat, 35 mission, 11
dialogue, one team-XP window and one command parser. No source remains in
`SharedGameplay`. Their original paths, fingerprints and consumers are retained
in the implementation audit; deleted dependent consumers have a separate list.

The broader review records 69 additional affected files: 63 deletions and six
partial removals/replacements. This conservative accounting includes dependent
orchestration, not 69 claims of literal source copying. The removed mission and
dialogue adapters cannot invoke the former implementation through a DAO.

Unavailable gameplay: player weapon attacks/hit resolution; mission generation,
quest objectives and rewards; generated mission worlds/corpses; dialogue and
dialogue trade; old team-XP eligibility; non-container and item-on-item use;
new player/NPC nano casts, team-heal pulses, team warps and area-taunt execution.
NPC activation, presentation and native AI are not a claim of combat parity.
Native per-player kill contribution arithmetic is distinct from team eligibility.
Native corpse gameplay is outside this cutover's accepted operational contract.

Unsupported request handlers reject before costs, inventory writes, rewards or
world creation. Only the current admitted owner receives unavailability text;
stale/pre-admission sessions receive no response. Repeated requests remain
non-mutating. Attack/cast success tests have not been counted as acceptance after
their execution paths were retired.

## Preserved integrity and saved data

Login/admission and ownership checks, character hydration/save, item instance
ownership, movement/equipment/trades, zoning, logout/reconnect and restart remain
required. No database schema change or production operation is part of this work.

Existing mission rows are not deleted, expired or rewarded. `SavedMissionLocation`
projects an owned saved exterior from exactly one matching binding/offer, with
positive identities and finite coordinates. It cannot allocate a world, consume
a key or advance a mission. Missing/ambiguous/foreign/invalid data rejects entry
without rewriting the saved location. A later ordinary character snapshot owns
any persistence of a successfully recovered exterior.

`IMissionDao` retains persisted DTO, clone and transaction-interface shapes. Its
similarity to the old repository interface is explicitly allowed saved-data
compatibility, containing no engine/session objects or gameplay orchestration.
SQL implementations retain transactions, ownership, row/version checks and
unknown-outcome handling. Fixed corpse-credit and mission/corpse-lifetime gameplay
bounds were removed; nonnegative amounts and valid ordered timestamps remain.
Disposable fixtures exercise values outside those removed gameplay bounds.

Existing nano rows retain duration, identity, owner and stat/appearance projection.
Hydration never replays instant healing. Saved rows still cancel and expire.
Unknown commit outcomes quarantine/close the actor; known rollback preserves
projection and rows. Saved retired area effects retain duration without executing
heal/taunt behavior. New casts cannot enter the removed scheduling/completion path.

## Independent replacements and editable data

- `MissionLevelData` loads validated immutable CSV rows. The historical generator
  command validates data and emits no C#. Quality/token columns change on reload
  in the same binary; loading the table does not enable missions.
- `CharacterRules.json` and `Progression.json` hold former resource/progression
  tables. The old calculators and linked skill/XP tables are outside the compile
  graph. Immutable evaluation and same-binary edits are tested; duplicate/null/
  malformed/ambiguous rule inputs reject.
- Weapon low/high identity bindings reside in `ItemBehavior.json`.
- The method comparison found a 0.976 structural match for the old locality
  configuration method, including renamed identifiers. It was replaced by
  immutable `Locality.json` loading plus explicit XML overrides. Zero XML fields
  mean unspecified; inconsistent/negative settings reject instead of invoking
  the copied repair-to-default algorithm.
- Command token parsing is independently implemented around whitespace/NUL and
  optional command prefixes. Existing session/GM checks remain at dispatch.

## Origin and content review

The current conservative project-reference source graph has 602 C# files and
zero unresolved project inputs. File comparison uses the 811-file pre-retirement
engine archive, identifier-normalized 32-token fragments and path history.
The secondary method screen compares 419 current bodies to 3,273 old bodies;
after the locality replacement it has no unresolved candidate. Short methods,
package binaries and other conditional compilation profiles are limitations of
this syntax screen, not silently counted as independently proven implementations.

Reviewed source fingerprints and rationale are in `NEWENGINE_LEGACY_ORIGIN_REVIEW.json`;
changed source loses its reviewed classification on the next origin scan. Shared
protocol/data/infrastructure classified native means native to the current stack,
not newly authored code. AOtomation's organization-command enum is a normalized
enum-shape false positive against a dialogue enum. No dialogue implementation is
present in that protocol declaration.

The compiled-content review distinguishes generated resource accessors/serializer
boilerplate from generated game content. Reviewed scalar special cases are zero
identity sentinels, metadata identity checks, saved-nano identity normalization and
relative portal destinations. They select no named NPC/item/quest content.
Native regeneration, spatial geometry, skill-quality interpolation and per-player
contribution arithmetic are generic mechanics; weapon assignments are editable.
The Roslyn graph/content guard and explicit origin review complement these scans.

The retained-source guard includes additional deleted-source and retired-method
fingerprints. Six mutation/negative-control cases cover a reintroduced path,
renamed copied parser, copied locality method, old namespace, embedded literal
table and legitimate protocol boilerplate. No old implementation is compiled as
a test helper to satisfy the guard or build.

## Test transition and acceptance

Mike explicitly approved retiring/replacing obsolete feature fixtures. The JSON
transition inventories record removed links, methods, consumers and stale wrapper
filters. Current tests retain packet round trips using captured bytes, login and
session security, inventory/DAO integrity, saved-state compatibility and rejection
without mutation. The AOtomation repository-root resolver now selects this
NewEngine checkout instead of accidentally walking to the parent checkout.

Tested public source: `d265e6188c6642004eb9c4ddb2a5254b83840cad`.
The source purge is `d5f8ace4`; the approved obsolete fixture retirement is
`d265e618`. Both were pushed to the isolated branch. Subsequent completion
receipt/project-state edits contain no runtime or test changes.

Final exact-source acceptance:

| Gate | Result |
| --- | --- |
| Full Windows build and all 12 mandatory stages | PASS |
| NewEngine tests and offline startup | PASS, 520 tests |
| AOtomation tests | PASS, 339 tests |
| Retained source guard and six mutation/negative controls | PASS |
| Content architecture and DAO guards | PASS |
| Dependency inventory | PASS, zero Legacy edges |
| Disposable schema, explicit migration, failed-copy rollback and two restarts | PASS |
| Authenticated login, zoning, logout/relogin, saved morph cancellation and process restart | PASS |
| Exact-source native restore/build/tests/publication/package | PASS, 520 NewEngine tests |

The Windows output freeze compared all **5,027** files byte-for-byte. NewEngine
SHA256 is `25e2c52d825878cd234f3b556fc1fa61487c2cbf549ddc54bbcbee393730147e`;
LoginEngine SHA256 is `bc0488d5a33f100774d8ba0e0890b367ad885e5c6cbfee7500a4c8cfcd29ae98`.
Both binaries retained those hashes after disposable acceptance. Schema restart
checks preserved 40 stat rows and two items; the final connected snapshot preserved
44 stat rows and six items with the character offline. These are disposable fixture
counts, not production population counts. Container/network residue was none.

Private native source `c30ec82dcbd8ba9220e20ef0e86a23f95d081831` combines the tested
public source with private operations `2a9287d4d72367f86d376b80183b7620d37ef554`.
Native build/test/package acceptance passed. The archive contains no Legacy engine
executable. Exact native engine/archive hashes and manifest identities are in
`NEWENGINE_RETAINED_LEGACY_ACCEPTANCE.json`; private content paths and private
tooling are not copied into this public receipt.

An initial native container attempt stopped before build because its base image
lacked git-lfs. Installing the documented prerequisites inside the disposable
container resolved it; the complete rerun passed. That stopped attempt is not
counted as acceptance. No source workaround or production change was made.

Remaining risks: optional systems listed above are deliberately unavailable;
official-client gameplay was not exercised; the DAO guard retains three existing
provider exceptions with zero new violations; static-origin analysis has the
stated scope limits. This is an accepted source/package candidate, not a production
deployment. Saved-data DTO/interface compatibility remains separately allowed.

## Required final accounting

```text
KNOWN_RETAINED_LEGACY_FILES=57
ADDITIONAL_REMOVED_OR_REWRITTEN_FILES=69
RETAINED_LEGACY_FILES_DELETED=57
RETAINED_LEGACY_FILES_REPLACED_WITH_DATA=1
RETAINED_LEGACY_FILES_CLEAN_REIMPLEMENTED=1
RETAINED_LEGACY_FILES_MOVED_TO_PERSISTENCE_COMPATIBILITY=0
RETAINED_LEGACY_IMPLEMENTATIONS_REMAINING=0
SHAREDGAMEPLAY_RETAINED_LEGACY_FILES=0
RETAINED_LEGACY_COMBAT_FILES=0
RETAINED_LEGACY_MISSION_FILES=0
RETAINED_LEGACY_DIALOGUE_FILES=0
RETAINED_LEGACY_TEAM_XP_FILES=0
RETAINED_LEGACY_CHAT_COMMAND_FILES=0
MISSION_LEVEL_TABLE_COMPILED_IN_CSHARP=NO
MISSION_LEVEL_TABLE_EDITABLE_WITHOUT_RECOMPILE=YES
GENERATED_GAME_CONTENT_CSHARP_FILES=0
RUNTIME_GAME_CONTENT_HARDCODE_VIOLATIONS=0
RUNTIME_CONTENT_SPECIFIC_SPECIAL_CASES=0
LEGACY_MISSION_ORCHESTRATION_BEHIND_DAO=0
LEGACY_BEHAVIOR_FALLBACKS=0
LEGACY_GAMEPLAY_COMPATIBILITY_TYPES=0
SAVED_DATA_COMPATIBILITY_PRESENT=YES
SAVED_DATA_COMPATIBILITY_CONTAINS_GAMEPLAY_CODE=NO
LEGACY_NEAR_COPY_REPLACEMENTS=0
LEGACY_BEHAVIOR_AS_SOLE_TEST_ORACLE=0
RETAINED_57_FILES_IN_COMPILE_GRAPH=0
PRODUCTION_MODIFIED=NO
```

Deleted-file counts describe physical removal. The two replacement counts describe
the disposition of those deleted files, not additional deleted files. One current
source is explicitly classified directly retained **data-only** DTO/interface
compatibility; it is not hidden in the gameplay-implementation zero.

| Decision | Result |
| --- | --- |
| All 57 relocated files individually accounted for and reviewed | YES |
| Retained combat, missions and dialogue removed | YES |
| Retained team-XP window and chat parser removed | YES |
| SharedGameplay preserves a Legacy implementation | NO |
| Mission graph moved out of compiled C# | YES |
| Generated game-content C# remains | NO |
| Old orchestration remains behind DAO | NO |
| Near-copy replacements remain | NO |
| Runtime game-content hardcoding remains | NO |
| Legacy gameplay fallback remains | NO |
| Unsupported request paths reject before mutation | YES |
| Saved-data compatibility remains separately identified | YES |
| Automated login/inventory/zoning/persistence acceptance | PASS |
| Production modified | NO |

```text
RETAINED_LEGACY_IMPLEMENTATIONS: 0
RETAINED_57_FILES_IN_COMPILE_GRAPH: 0
RUNTIME_GAME_CONTENT_HARDCODING: 0
GENERATED_GAME_CONTENT_CSHARP: 0
LEGACY_GAMEPLAY_FALLBACKS: 0
LEGACY_NEAR_COPY_REPLACEMENTS: 0
NEWENGINE_ARCHITECTURE_PURGED_OF_LEGACY_IMPLEMENTATION: YES
PRODUCTION_MODIFIED: NO
```

## Artifact index

- `NEWENGINE_RETAINED_LEGACY_IMPLEMENTATION_AUDIT.json`: 57 original rows,
  additional removals/replacements, fingerprints and saved-data exceptions.
- `NEWENGINE_LEGACY_ORIGIN_SCREEN.json`, `NEWENGINE_LEGACY_ORIGIN_REVIEW.json`
  and `NEWENGINE_LEGACY_METHOD_SCREEN.json`: provenance and comparison evidence.
- `NEWENGINE_COMPILED_CONTENT_AUDIT.json` and
  `NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json`: compiled source/content review.
- `NEWENGINE_CLEAN_REIMPLEMENTATION_BACKLOG.json` and
  `NEWENGINE_SUPPORTED_FEATURE_MATRIX.json`: honest current boundaries.
- `NEWENGINE_RETAINED_LEGACY_ACCEPTANCE.json`: tested source/package identities,
  final gate results and disposable-state preservation receipts.
- `NEWENGINE_*TEST_TRANSITION.json`, `NEWENGINE_RETIRED_TEST_FILTERS.json`,
  `NEWENGINE_REMOVED_TEST_COMPILE_LINKS.json` and
  `NEWENGINE_PURGE_REMOVED_CONSUMERS.json`: retired fixture/consumer accounting.

Source, test and tool paths inspected are recorded by these inventories. The
final commit diff is the authoritative changed-file list; no private content or
private platform tooling belongs in that public diff.
