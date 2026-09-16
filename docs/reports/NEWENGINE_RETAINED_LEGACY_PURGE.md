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

Current worktree verification: NewEngine **520 PASS** including offline startup;
expanded retained guard and six mutations **PASS**; DAO guard **PASS**; cutover
inventory **PASS**, zero Legacy dependency edges. Earlier AOtomation **339 PASS**
and disposable schema/connected acceptance **PASS** are intermediate receipts;
they must not substitute for final exact-source acceptance.

Exact-source Windows/mandatory acceptance, final disposable schema/connected
acceptance and private native package acceptance are **PENDING**. This candidate
is not authorized for production deployment. Final immutable receipts and tested
source identities will be appended after those gates run.

## Artifact index

- `NEWENGINE_RETAINED_LEGACY_IMPLEMENTATION_AUDIT.json`: 57 original rows,
  additional removals/replacements, fingerprints and saved-data exceptions.
- `NEWENGINE_LEGACY_ORIGIN_SCREEN.json`, `NEWENGINE_LEGACY_ORIGIN_REVIEW.json`
  and `NEWENGINE_LEGACY_METHOD_SCREEN.json`: provenance and comparison evidence.
- `NEWENGINE_COMPILED_CONTENT_AUDIT.json` and
  `NEWENGINE_CONTENT_ARCHITECTURE_GUARD.json`: compiled source/content review.
- `NEWENGINE_CLEAN_REIMPLEMENTATION_BACKLOG.json` and
  `NEWENGINE_SUPPORTED_FEATURE_MATRIX.json`: honest current boundaries.
- `NEWENGINE_*TEST_TRANSITION.json`, `NEWENGINE_RETIRED_TEST_FILTERS.json`,
  `NEWENGINE_REMOVED_TEST_COMPILE_LINKS.json` and
  `NEWENGINE_PURGE_REMOVED_CONSUMERS.json`: retired fixture/consumer accounting.

Source, test and tool paths inspected are recorded by these inventories. The
final commit diff is the authoritative changed-file list; no private content or
private platform tooling belongs in that public diff.
