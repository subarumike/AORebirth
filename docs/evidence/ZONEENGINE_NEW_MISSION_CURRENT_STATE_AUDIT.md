# Current ZoneEngine_New mission audit

Audit date: 2026-10-09. Documentation and offline audit tooling only.

The current engine already has connected roll, offer/fee persistence, quest, key,
procedural dungeon, zoning and authored-mission mechanisms. Preserve them. The
first generated-mission blocker is a source/data mismatch: the selector contains
140 locations across 25 playfields, while the selected entrance catalog contains
one PF540 sample. None of the 140 locations is in PF540. Consequently, normal
generated offers cannot pass the current acceptance entrance lookup with these
audited inputs. This is **PROVEN from current source/data**, not an observed live
client failure.

Connecting the new catalog is necessary but insufficient. Only Kill Person has a
usable generated objective definition, and that definition uses a placeholder
NPC hash without unique mission-instance qualification. The other four generated
objective paths are missing. Completion and reward persistence are separate;
live dungeon state is recreated after unload/restart. These are targeted gaps in
existing systems, not justification for replacing the mission architecture.

## Authority, scope and evidence

| Identity | Exact commit |
| --- | --- |
| Audit branch starting foundation | `f07bb3c1a99218433e7d459c95ba29ccb22c36b2` |
| Current engine, fetched public `origin/master` | `bddea56ca2d47c5eb64ed53f6e9efd279ff4f694` |
| Destination foundation source | `f07bb3c1a99218433e7d459c95ba29ccb22c36b2` |
| Audit output branch | `codex/zoneengine-new-mission-current-state-audit` |

The output branch preserves the destination foundation. Its older runtime is
**not** the audited current engine. Runtime citations below link to exact public
engine SHA; foundation citations belong to the separate foundation SHA. No
runtime source from either line was merged, copied or edited for this audit.

All seven requested reports were read completely. The reconstruction,
eligibility, duplicate and catalog foundation reports establish 2,242 client
placements, 812 observed destinations and 92,830 raw-backed resolved observations.
The September reconciliation, gap-closure and gameplay checkpoint reports are
historical evidence. Their `GeneratedMissionAcgService`, `GeneratedMissionWorld`,
`MissionPlayfield` and captured bundle claims do not describe the current
`QuestDungeonService`/`QuestDungeonPlayfield` owner. No retired ZoneEngine runtime
source was used as current authority.

**OBSERVED** means inspected source, data or retained evidence. **PROVEN** means
the bounded conclusion follows from that source/data. **INFERRED** means a risk
or proposed future behavior. **UNVERIFIED** includes current live behavior,
runtime operational BD/registry key, complete retail eligibility/weights and a
live-returned mission QL field. A source-reachable component is not a successful
live end-to-end run.

The machine-readable [component inventory](../generated/missions/zoneengine-new-current-state-audit/component-inventory.json)
contains every inspected file, literal content-search coverage for all requested
terms in all three requested source scopes, consumers, tests and references.
Search hits are not automatically treated as components. The
[manifest](../generated/missions/zoneengine-new-current-state-audit/audit-manifest.json)
pins input files to source SHAs and hashes every output. The selected private
entrance file has the same JSON as the public sample. Eleven selected policy,
shell and QL inputs are byte-identical to their public blobs; only logical labels
and hashes are published, with no private payload or machine paths.

## Current pipeline and owners

[The pipeline map](../generated/missions/zoneengine-new-current-state-audit/mission-pipeline-map.json)
gives file, class, method, input, output, data source, persistence owner, tests and
runtime reachability for every stage. Registration is in
[Program](https://github.com/subarumike/AORebirth/blob/bddea56ca2d47c5eb64ed53f6e9efd279ff4f694/AORebirth/Server/ZoneEngine_New/Program.cs#L247),
with actual handlers and callbacks traced rather than inferred from class names.

| Stage | Actual current path and boundary |
| --- | --- |
| Terminal/request | Spawned `MissionTerminal`; registered `QuestAlternativeMessageHandler.Handle` validates actor/terminal/range and obtains sliders. Player X/Z are passed as the arguments named terminal X/Z. |
| Slider/QL/type | `MissionRollSliders.TryCreate` → editable `MissionLevelRuntime` lookup → `MissionRollEvidenceCatalog.SelectTypeMix`. |
| Destination/five offers | `GeneratedMissionRollService.Generate` → `MissionRollLocations` → C# `MissionLocationPool.Spots`; compatible captured shells form five offers with distinct offer IDs. |
| Wire/fee/offers | `GeneratedMissionRollProjection.Create` freezes wire fields. `GeneratedMissionService.PublishOffers` joins offer publication and fee debit through `IGeneratedMissionDao`; response follows successful publication. |
| Acceptance/binding | `CreateQuestMessageHandler` → `QuestDungeonService.AcceptOffer`; owned unexpired offer must match current entrance catalog. Parameters and definition are frozen before current quest/key transaction. |
| Mission/key persistence | `ICharacterQuestStore` → `MySqlCharacterQuestDao.TryAcceptOfferQuest`: offer claim, `generatedquests`, `characterquests`, key item and `questdungeonkeys` link in one transaction. |
| Instance/layout | `QuestDungeonIds` plus frozen seed/version → `DungeonLayoutGenerator` → existing dungeon generator/collision library. |
| Entry/world | Exterior trigger → `QuestDungeonService.TryEnter` → `PlayfieldManager.GetOrCreateQuestDungeon` → `QuestDungeonPlayfield`; key and quest validation gate zoning. |
| Objective | Kill-credit callback → `QuestService.OnNpcKilled`; LookAt → `OnNpcTargeted`. Generated kill objective is partial; other generated definitions cannot complete normally. |
| Completion/reward | `QuestService.Complete` saves Completed first, then `GrantReward` mutates cash/XP/items separately. `OnQuestEnded` retires links/keys and releases world. |
| Reconnect/restart | `ResolveLogin`, quest restore and key restore reload durable definition/progress/links. An absent world rebuilds; previous NPC/door state is not restored. |
| Expiry/cleanup | Minute expiry sweep, character-log expiry, abandonment, one-day old-definition purge, ten-minute idle world sweep and occupied-world protections are present. |

`AuthoredQuestService`/`AuthoredMissionProgression` remain a separate active
`IMissionDao` route with transactional authored state, inventory, stats, reward
ledger and account cooldown behavior. `QuestPropService` validates authored prop
identity/template/transform and interaction ownership. Knubot assignment and
completion also call current template `QuestService`. Keep these owners
distinct. Generic authored item/prop support does not prove generated Find Item
or Repair support.

The retained older generated DAO acceptance/observation/completion APIs are not
the current normal acceptance route. `ZoneLoginHandler` still reads old accepted
bindings to evacuate saved characters from the older mission playfield range;
retained compatibility data must not be deleted just because new missions use
another owner.

## Entrances, exact comparison and proximity

Current source is `AORebirth/GameData/MissionEntrances.json`, selected through
the existing GameData mechanism. Current model: implicit identity type
`MissionEntrance = 0xDAC6` plus signed `Instance`, playfield, float XYZ,
HeadingX/Y/Z/W and name. Comparing an instance uses its unchanged 32 bits, not
numeric sign as a different identity.

`MissionEntranceCatalog` indexes by instance and supports `TryGet` and
`TryFindAt`. It is **not** the rolling source. It **is** used for acceptance
binding, entry and return/login exterior resolution. See the exact
[comparison with per-row bits](../generated/missions/zoneengine-new-current-state-audit/current-entrance-catalog-comparison.json)
and [entry flow](../generated/missions/zoneengine-new-current-state-audit/entrance-instance-flow.json).

| Exact comparison | Count |
| --- | ---: |
| Current engine entrances | 1 |
| Proven catalog entrances | 2,242 |
| Complete identity matches | 1 |
| Current only / proven only | 0 / 2,241 |
| Exact PF+XYZ binary32 matches | 0 |
| Coordinate mismatches | 1 |
| Raw rotation slot mismatches | 1 |
| Name-only / all name differences | 0 / 0 |
| Playfield mismatches / identity conflicts | 0 / 0 |

No tolerance, rounding, coordinate normalization or name fallback hides a
discrepancy. Raw HeadingX/Y/Z/W versus RotationComponent0/1/2/3 inequality is
**not proof of a semantic orientation error**: the rotation convention remains
unresolved. Preserve both representations for later evidence-backed conversion.

The only current `TryFindAt` consumer is
[AcceptOffer](https://github.com/subarumike/AORebirth/blob/bddea56ca2d47c5eb64ed53f6e9efd279ff4f694/AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/QuestDungeonService.cs#L154).
It binds an offered destination to a catalog entrance within one horizontal
unit, ignores Y, and permits later equal-distance candidates to replace earlier
ones. Classification: **SAFE_BUT_WEAKER_THAN_PROVEN_DATA** for accepted-offer
binding; it is not the historical exact WorldPos reconstruction rule. The
catalog's exterior XYZ then supplies the frozen binding.

Terminal-use distance, player-to-door distance, objective interaction distance
and entry-trigger range are operational mechanics:
**VALID_ENGINE_MECHANIC_NOT_CLAIMED_AS_RECONSTRUCTION**. They should remain
separate from exact placement identity. No proximity implementation was changed.

`DungeonExitDoors.Find` also uses a 1.2m X/Z tolerance for static/RDB room-door
connections, with a first-exit landing fallback. This is a shared geometry
mechanic, **VALID_ENGINE_MECHANIC_NOT_CLAIMED_AS_RECONSTRUCTION**, separate from
generated mission destination identity. Generic terminal use checks template
requirements through `StaticDynel.CanBeginUse`; the roll handler applies a
separate geography/faction policy. Template-specific faction restrictions were
not established by this audit.

## Destination selection and pool membership

The exact [selection audit](../generated/missions/zoneengine-new-current-state-audit/current-destination-selection.json)
records every input and rule. It filters using character level, faction and
terminal geography. It does not directly filter candidates by expected QL,
terminal identity, mission type or secondary sliders. Terminal identity still
has ownership/return-target uses outside destination selection. Shared RNG
consumption can affect results without being an explicit filter.

The current pool is **140 hard-coded rounded coordinate/offset rows**, not the
observed 812, the full 2,242 or `MissionEntrances.json`. Complete placement IDs
are absent. Exact PF+binary32 XYZ joins produce:

| Pool comparison | Inside | Outside exact join |
| --- | ---: | ---: |
| Observed 812 | 0 | 140 |
| Full 2,242 | 0 | 140 |

The 140 are **unresolved under the proven exact rule**, not proven invalid.
Neither proximity nor similar names can establish their intended identities.
The 812 is observed coverage, not complete eligibility.

Selection uses uniform candidate-index draws with filters and up to 48 retries
preferring unused Spot references, and above level 40, different playfields.
Fallback allows repeats: **strict cohort uniqueness is NO**. Therefore there is
no current blanket “forces five unique destinations” finding. The anti-repeat
bias is unsupported by the destination evidence; exact repeated destinations
within one cohort are proven possible. Distinct offer IDs remain necessary.

Low-level selection prefers a nonempty same-playfield pool. In particular, the
level ≤40 PF655 restriction conflicts with captured level-2 PF655 rolls spanning
three playfields. Faction/geographic filtering and broader fallback behavior
remain existing compatibility policy or reconstructed inference, not a recovered
retail eligibility law. There is no measured numeric weight table; rejection
and retries mean the final distribution is not established as uniform.

`EntranceLow/High`, then `BuildingLowId/HighId`, carry signed WorldPos X/Z
conversion offsets in this path. They are not proven placement IDs, item
template IDs or operational BD keys. The compact placement catalog does not
provide these offsets. Future integration must retain/derive them from proven
WorldPos evidence or accepted data; filling them with zero or the placement
instance would corrupt semantics.

## QL, sliders, type and rewards

[QL audit](../generated/missions/zoneengine-new-current-state-audit/ql-system-audit.json)
separates character level, wire difficulty detent, static expected QL,
server-generated quality and unproven live-returned QL.

The active QL mechanism reads editable `Missions/Source/MissionLevels.csv` and
`MissionLevelRules.json`: 220 rows, 11 difficulty columns, character level
clamped 1–220, wire difficulty 1–11 mapped to index 0–10, generated quality
1–250. This is accepted repository lookup behavior, not a newly recovered live
formula. The neutral column equals character level. CSV token lookup methods
have no current engine consumer found. The compiled `MissionLevelGraphData.g.cs`
duplicate has no current consumer; its optional separate removal is not required
for catalog integration. Do not remove the CSV.

`QuestInfo.Quality` serializes the server-selected value and projection freezes
it. That property name does not establish a decoded retail response QL field.
`RollMixes.MissionQuality` and `RewardObservations.MissionQl` lack explicit
expected/static qualifiers; their observations must not be promoted to live QL
proof. No named active `LiveReturnedMissionQl` decoder was found.

The [policy audit](../generated/missions/zoneengine-new-current-state-audit/mission-roll-policy-audit.json)
classifies each rule and traces its evidence limits:

| Rule | Current behavior | Authority limit |
| --- | --- | --- |
| Six secondary sliders | Decode signed percentages −100…100; two configured profiles; unresolved combinations rank as Neutral | Wire decoding and existing fallback are different claims; fallback is compatibility policy. |
| Type cohort | Choose nearest of 21 configured five-type cohorts, then random tie; all five type values occur | Recorded cohorts are observations; nearest-match selection is a derived policy, not retail probability. |
| Offer shell | Select compatible same-type shell among 13 configured captured bodies | Packet shape does not prove type-specific gameplay. |
| Cash/XP | 108 deduplicated observation pairs; nearest same-type record, no interpolation | Pair values have evidence; applying them outside observed conditions is inference. |
| Reward item | Packaged catalogs, scalable QL range, fixed-nano tolerance 10 fallback, exclusions | Item identity does not establish complete retail mission rollability or weights. |
| Fee/lifetime | `max(1, level)`; configured 172,800 seconds | Retained engine policy, not new retail timing evidence. |
| Rare loot | Exclusion predicate active; probabilistic `TryRoll` has no active consumer found | Dormant percentage/near-QL policy cannot establish current dungeon loot support. |

## Five-type capability matrix

“Complete active” below means the bounded registered stage exists in current
source; it does not mean retail correctness, audit-time execution or that an
earlier blocker was bypassed. Acceptance is partial for every type because the
current catalog blocks normal offers. Full definitions and line references for
all 55 cells are in the [matrix](../generated/missions/zoneengine-new-current-state-audit/mission-type-capability-matrix.json).

| Type | Roll | Wire | Accept | Persist | Dungeon content | Objective | Reward | Reconnect | Restart | Expiry | Cleanup |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Find Item | COMPLETE_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | MISSING | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE |
| Find Person | COMPLETE_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | MISSING | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE |
| Kill Person | COMPLETE_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE |
| Repair | COMPLETE_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | MISSING | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE |
| Return Item | COMPLETE_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | MISSING | PARTIAL_ACTIVE | PARTIAL_ACTIVE | PARTIAL_ACTIVE | COMPLETE_ACTIVE | PARTIAL_ACTIVE |

Find Person has a generic target callback, but empty generated NPC criteria
cannot match. Kill Person uses the fixed `LE01` hash and generic hash matching;
there is no unique objective-actor/owning-dungeon fence. Find Item, Repair and
Return Item lack their generated event processors and materialized objective
content. Reward code is shared but unreachable through missing normal
objectives, and its durability is partial even when called.

## Entrance, instance, dungeon and restart

[The entrance flow](../generated/missions/zoneengine-new-current-state-audit/entrance-instance-flow.json)
distinguishes static placement identity, accepted quest identity, physical key
identity, persisted quest/key link, internal dungeon/playfield identity, actual
world and zone destination. Entry resolves the exterior identity, carried
persisted key with a durable quest link, and live generated definition; it checks
location/range and lifecycle conditions. It does not require equality with the
accepted quest's owner or team membership. Key duplication/sharing is an explicit
engine mechanism. Name alone is not entry authority. Interior exits use the
exterior/return path.

`ResolveLogin` immediately returns an existing live dungeon before checking
quest expiry/completion. When no live world exists, it rebuilds an active quest
or returns to its exterior if the persisted quest and catalog resolve. Missing
quest/catalog falls back to configured respawn. This branch distinction must be
retained in restart acceptance checks; closed-login evacuation is not universal
at this method boundary.

Classification: **ENGINE_INTERNAL_CONTRACT**, consistent with known static
placement evidence only where the inputs agree. Retail-compatible operational
BD/registry authorization remains **UNPROVEN**. Do not substitute an exterior
identity, offered WorldPos offset or internal playfield number for that missing
retail bridge.

[The generated dungeon map](../generated/missions/zoneengine-new-current-state-audit/generated-dungeon-flow.json)
traces frozen seed/version → `DungeonLayoutGenerator` → shared generator rooms →
collision builder → `QuestDungeonPlayfield`. It has real procedural geometry,
doors, ambient NPC spawning, entry/exit and world release. Historical claims of
an empty placeholder playfield are no longer an accurate current description.

Current temporary content choices include style 324, two floors, size 16,
five-percent locks, ambient `ZK3X` with 35% placement chance and `LE01` kill
target. They need content/evidence work, not a replacement geometry framework.
There is no generated chest materialization in the current world builder.
NPC creation follows `HashSpawnSystem.AddPlacedDistrictPoint` → spawn branches →
`SpawnService.SpawnMob`. Later `Random.Shared` heading/radius jitter means exact
NPC poses are not fully deterministic from the layout seed.

The current class explicitly keeps live world state ephemeral. Definition,
progress, seed/version and key links survive; NPC HP/death, door state and live
objective objects do not. Rebuilt geometry does not imply restored world state.
Retain current generator/versioning and idle/occupied-world protections; define
the smallest required continuity before proposing persistence changes.

## Persistence and failure boundaries

[The table map](../generated/missions/zoneengine-new-current-state-audit/persistence-table-map.json)
documents 16 mission tables, four shared dependency tables and 79 runtime DAO
call sites, including keys, readers/writers, transaction boundary, restart,
cleanup and exact SQL/schema authority. No database was queried or changed.

| Family | Tables and active role |
| --- | --- |
| Authored mission owner | `missionstates`, `missionobjectiveprogress`, `missionobjectiveobservations`, `missionflags`, `missionaccountflags`, `missionrewardledger`; authored atomic operations remain connected. |
| Current generated offers | `generatedmissionbatches`, `generatedmissionoffers`, `generatedmissionsequences`; roll/fee publication is active, offer claim also occurs in current quest DAO. |
| Current template/generated quests | `characterquests`, `generatedquests`, `questdungeonkeys`; current acceptance/progress/key owner. |
| Retained former generated lifecycle | `generatedmissionbindings`, `generatedmissionartifacts`, `generatedmissionobjects`, `generatedmissionobservations`; available DAO capabilities and old-state readers must be distinguished from new-mission writers. |
| Shared dependencies | `characters`, `stats`, `item_instances`, `item_instance_id_sequence`; ordinary character/inventory owners and specific mission transactions. |

Offer publication and current acceptance are different transactions. Current
acceptance uses `MySqlCharacterQuestDao` at ReadCommitted and includes key item
and link writes. `ItemInstanceIdAllocator` leases blocks of 10,000 IDs through
the inventory repository independently; a leased ID is not a committed quest.

Two high-priority source findings must precede a safe end-to-end milestone:

1. [QuestService.Complete](https://github.com/subarumike/AORebirth/blob/bddea56ca2d47c5eb64ed53f6e9efd279ff4f694/AORebirth/Server/ZoneEngine_New/Core/Quests/QuestService.cs#L346)
   saves completion before `GrantReward`. `GiveItem` warns and returns when
   inventory is full. The split is **PROVEN**; actual reward loss incidence is
   **UNVERIFIED**. Existing authored atomic rewards do not automatically cover
   this owner.
2. [MySqlCharacterQuestDao.Transaction](https://github.com/subarumike/AORebirth/blob/bddea56ca2d47c5eb64ed53f6e9efd279ff4f694/AORebirth/Libraries/Source/AORebirth.Database/Domain/Quests/MySqlCharacterQuestDao.cs#L545)
   calls raw `Commit`. `SharedCharacterPersistence.Run` translates only the
   established typed ambiguous-commit exception, while acceptance catches the
   translated engine exception. The current DAO does not emit that contract.
   The mismatch is **PROVEN**; injected failure behavior was **NOT RUN**.

## Content and test audit

[C# content findings](../generated/missions/zoneengine-new-current-state-audit/csharp-content-findings.json)
classify embedded destination coordinates/offsets, location policy, shell/reward
filters, placeholder hashes, style/size/floor/lock/spawn choices, key templates,
identity ranges, journal values and shared schema/mechanics. Protocol/schema
constants are separated from content that should move to editable data and
unproven hard-coded content. Existing editable policy/catalog data is preserved;
no content was moved in this task.

[Test inventory](../generated/missions/zoneengine-new-current-state-audit/mission-test-inventory.json):
**791 test/validation records across 107 files, all NOT_RUN**. The count includes
individual test declarations and named assertions in direct-DAO smoke programs;
it is not a claim of 791 independently executed unit tests. Each record includes
component, intended assertion/proof limits, runtime-path scope, external DB use,
evidence assumption and source status. Foundation tests have their own source
SHA. Previous foundation successes remain historical checkpoints.

Current component-harness tests do not run a full live pipeline. Direct-DAO
smokes exercise a database only when explicitly run. Source/fixture checks do
not prove runtime wiring. `GeneratedMissionServiceTests` references removed
Accept/Complete APIs; materialization fixtures reference removed
`GeneratedMissionWorld`/`GeneratedMissionNpcCharacter`. These are source-contract
mismatches, not audit-time test failures. The level-4 Neutral same-PF test
preserves a local policy; it does not establish a universal retail rule. The
underlying level ≤40 PF655 restriction is contradicted by level-2 Omni captures.
No current direct
acceptance/reward test was found that establishes the whole
`QuestDungeonService` → `MySqlCharacterQuestDao` route.

## Keep map and smallest roadmap

The [disposition map](../generated/missions/zoneengine-new-current-state-audit/component-disposition.json)
assigns exactly one disposition to each of 67 significant components:

| Disposition | Count |
| --- | ---: |
| KEEP_AS_IS | 44 |
| KEEP_AND_CONNECT_NEW_DESTINATION_CATALOG | 3 |
| KEEP_WITH_SMALL_EVIDENCE_CORRECTION | 11 |
| KEEP_BUT_REPLACE_DATA_SOURCE | 3 |
| DEFER_PENDING_EVIDENCE | 4 |
| REMOVE_OBSOLETE_DUPLICATE | 1 |
| TEST_ONLY | 1 |
| REFACTOR_LATER / UNKNOWN_NEEDS_FOLLOWUP | 0 / 0 |

The three catalog-connection components are the roll service, frozen projection
and entrance catalog. These dispositions preserve their mechanisms while
connecting exact data. The correction category identifies bounded follow-up
work; it does not imply every correction is trivial. The unused compiled QL
duplicate is optional separate cleanup. Nothing was removed.

[The roadmap](../generated/missions/zoneengine-new-current-state-audit/integration-roadmap.json)
contains exact files/classes, current behavior, required change, evidence,
tests to retain/change/add, risk and acceptance criteria for each phase:

1. Connect the foundation catalog at the existing entrance seam, adapting its
   older Utility library ownership to current shared source. Use the existing
   GameData root selector and preserve accepted-record compatibility.
2. Replace rounded destination content with exact references and a bounded
   evidence-backed policy. Preserve WorldPos offsets, allow destination repeats,
   correct contradicted geography and avoid inventing eligibility or weights.
3. Repair ambiguous-commit handling and completion/reward durability within
   current persistence owners. This can proceed independently of catalog work.
4. Finish one evidence-backed Kill Person lifecycle using editable content and
   exact objective actor/instance binding. Reuse current kill callbacks,
   generator, key, entry and exit mechanisms.
5. Establish the required world restart contract and implement only proven
   necessary continuity. Any schema change needs separate approval.
6. Add the remaining objective types independently through current services,
   with their own content and interaction evidence.

The observed-812 metadata supports coverage and condition analysis; it does not
silently become a whitelist or weighting table. Full retail destination policy,
operational BD, full slider/reward law and live-returned QL remain deferred.
All five types already have roll/wire support. None is established as a complete
ordinary end-to-end retail-style mission with the current catalog. Catalog work
alone must not be reported as five-type completion.

## Reproduction and change boundary

New tooling reads immutable Git blobs at both pinned SHAs and canonicalizes the
reviewed semantic JSON. It independently recomputes exact entrance/pool joins,
content-search inventory, structural consistency and manifest hashes. It does
not mechanically prove all prose interpretations or rerun gameplay tests.

From the repository root, using the already selected GameData directory:

```bat
Tools\zoneengine_new_mission_current_state_audit.cmd generate --configured-entrances "<selected-GameData>\MissionEntrances.json"
Tools\zoneengine_new_mission_current_state_audit.cmd generate --check --configured-entrances "<selected-GameData>\MissionEntrances.json"
```

The configured entrance input must match the audited public sample semantically;
a different dataset requires review, not silent overwriting of this audit.
No ignored local reviewer notes are needed to validate the committed artifacts.

Changed files: this report, the 15 requested JSON outputs, and the new Python
audit tool plus its `.cmd` runtime-selector wrapper. Existing task/status docs
and unrelated primary-checkout changes were preserved. No runtime, DAO, schema,
test, gameplay or content source was changed. No build, suite, database access,
capture, AO client, backend engine operation or deployment was performed.

Validation is limited to deterministic audit regeneration/check, source/input
hash verification, cross-artifact counts and schemas, diff whitespace and exact
change-scope review. The implementation roadmap is not implemented or authorized
by completion of this audit.
