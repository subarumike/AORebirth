# NewEngine editable content and Legacy bridge removal

## Scope and source

This change removes compiled per-content definitions from NewEngine and makes
validated editable data its world-content authority. Generic protocol handling,
mechanics, transaction ownership and player DAO persistence remain in runtime C#.

Starting source: `323652db3b07d9640dca5b084a3cfd807baa6b0c`, also the fetched
`origin/master` at task start. Work is on Mike's
`codex/data-driven-world-20260915` branch in
`build-verify/delmus-runtime-merge-20260914`. Delmus's branch was not changed.
Legacy remains present. Production was not modified; no schema migration or
production operation is part of this task. The previously recorded production
runtime is `88bed3bcef39da9dfbafb82222e9b128b3454598`; this task does not claim a new
live inspection of that deployment.

## Root cause

NewEngine used Accepted catalogs and linked Legacy content constructors as a
second content database. Those classes embedded actors, identities, transforms,
appearance, stock, rewards, quest routing and wire content. Hash spawns also
required an exact match to a separate reviewed placement corpus. A valid editable
spawn or template could therefore remain inactive because it lacked evidence
authorization. Shared combat definitions had a similar coupling between
structural validity and historical capture provenance.

## Content ownership after the change

| Content | Editable authority | Generic runtime consumer |
| --- | --- | --- |
| Placed NPCs, Scarlett, garden/subway/Arete actors | `AORebirth/GameData/WorldContent.json` | `WorldContentCatalog`, `WorldNpcFactory`, `NpcContentActivationService` |
| Stock, standalone shops and summon actors | `WorldContent.json` NPC/shop/summon records | Existing trade services and `SummonService` |
| Hash placements | Operator-generated `GameData/Playfields/<id>/Spawns.json` | `HashSpawnSystem`, `SpawnContentValidation` |
| Template/family/overlay values and weapon variants | Existing editable NPC template/family/overlay sources | `NpcTemplateCatalog`, `NpcTemplateValidation`, `NpcWeaponVariant` |
| Quest actions, rewards, handoffs, props, journal projections | `AORebirth/Server/ZoneEngine/Content/Runtime/interactions.json` | Existing quest registries and generic transactional action executor |
| Dialogue and visibility | Existing manifest/pack dialogue JSON and runtime bindings | Existing dialogue loader, catalog, router and session engine |
| Mission layouts, roll bodies, reward observations, NPCs, artifacts and corpses | `AORebirth/GameData/Missions/*.json` | Existing mission generation, DAO lifecycle and generic projections |
| Weapon/fist bands, item packages, vital effects and protected item identities | `AORebirth/GameData/ItemBehavior.json` | Existing item/inventory/combat mechanics |
| Nano bindings, masks, pulse tiers, exclusions, child links and visuals | `AORebirth/GameData/NanoMechanics.json` | Generic nano specializations and validated wire-template projection |

These extend the existing GameData and content-pack loaders. No new player-content
database or parallel persistence framework was introduced. Runtime JSON files are
published as content assets. Operator-supplied world/template payloads remain
outside public Git; the impact artifact contains aggregate counts and hashes.

The NPC export preserves 22 placed actor records, three standalone shops and
1,381 stock rows including the summon. The quest export preserves 18 actions,
16 dialogue bindings and 11 journal projections. Mission content retains five
captured layouts, 13 roll packet bodies, one roll template, 21 cohorts, 108 reward
observations and four rare-drop records. These are source migrations of existing
content, not newly invented packet behavior.

## Removed authorities and retained mechanics

The Accepted actor catalogs, `OfficialHashSpawnAuthorization`,
`NpcContentAcceptance`, named summon service and named quest-specific partials
are removed from NewEngine runtime. Generic activation still requires a registered
actor, valid source data, exact reference ownership and valid shop contents. Range,
conversation, transaction and stale-object checks remain enforced.

Content-bearing Legacy catalogs are no longer compiled or packaged into
NewEngine. Source required by Legacy and historical compatibility fixtures remains
available to those consumers. Shared DTOs and generic packet/gameplay mechanics
are classified separately in the bridge inventory; retaining them is not retaining
the content bridge layer.

Runtime validity checks use structure and supported mechanics. Historical combat
evidence validation remains a separate Legacy/offline audit path. Provenance fields
are preserved in migrated data but do not authorize spawning, combat or rewards.
DAO effect references retain stable transaction identities; they are not evidence
approval keys.

Unknown or malformed templates do not fall back to `AAAA`. The reserved diagnostic
placeholder cannot become an ordinary actor, attack target or AI combatant.
Configured event activation remains editable data. Unsupported mechanics reject
before effects/cost commit where applicable. Active bitmask projection currently
supports the transient MapsC stat; durable character stats are rejected by that
specialization's validator.

## Audit and guard

`NEWENGINE_RUNTIME_CONTENT_HARDCODE_AUDIT.json` records the complete discovered
NewEngine runtime source graph, hashes, candidate classifications, domain review,
historical content groups and current findings. This includes consumed libraries
and linked Legacy compile sources, not only the Mobs directory.

`NEWENGINE_LEGACY_CONTENT_BRIDGE_INVENTORY.json` records each content bridge's
consumers, specific content, retained mechanics, replacement data, replacement
service and deletion disposition. File-level semantic groups and individual syntax
findings are separate count units. The conservative static dependency graph is
not a proof of call reachability.

`Tools/run_newengine_content_architecture_guard.cmd --check` is now part of the
normal Windows build and exact-source acceptance. It checks contextual content
initializers, identity branches, named content types, embedded payloads and NPC
stat construction across the runtime source graph. There is no grandfathered
violation allowlist. Mutation tests exercise rejected NPC/stock/quest definitions,
reversed identity branches and identity switches, plus allowed protocol enums
and data readers. As with any static guard, a semantic review remains necessary
for future changes; a naming-based scan alone is not treated as complete proof.

## Editing and validation behavior

Change the relevant JSON under the configured GameData/content root, validate it,
then reload the catalog or restart the candidate server. No new live hot-reload
endpoint was introduced. Same-binary tests reload changed files and verify actor
placement/rotation/stats/appearance, stock, quest rewards, dialogue visibility,
summon selection/offset/lifetime, weapon bands and nano bindings. Invalid records
must fail validation or be explicitly skipped with diagnostics; they are not
converted into plausible default actors.

The test suite retains original wire/stat/stock fixtures to compare the exported
data with previously working behavior. Retirement of an item preserves its
historical row; active-row assertions distinguish retired rows from current
inventory. Quest grant publication order remains configurable data around the
same single durable transaction.

## Activation impact

The impact evaluator compares the prior authorization implementation with the
current validators and checks actual `HashSpawnSystem.Initialize` registration
counts for every supplied playfield. Input hashes, evaluator binary hashes and
all per-playfield counts are in
`NEWENGINE_DATA_DRIVEN_CONTENT_ACTIVATION_IMPACT.json`.

| Measure | Count |
| --- | ---: |
| Playfields evaluated | 630 |
| Source placement rows | 33,034 |
| Template-resolved rows | 25,819 |
| Newly eligible runtime placements | 24,942 |
| Resolved but configured event-inactive | 877 |
| Unresolved, excluding structurally invalid | 7,088 |
| Structurally invalid | 127 |
| Newly gameplay-accepted / READY | 0 |

The exclusive source-row partition is 25,819 structurally usable resolved rows,
7,088 unresolved rows and 127 invalid rows. The separate template-resolution
dimension has 7,215 unresolved rows, including invalid overlap. These are different
dimensions, not additive totals.

| Playfield | Newly eligible / source rows |
| --- | ---: |
| 4582 | 199 / 207 |
| 4544 | 1,036 / 1,048 |
| 800 | 24 / 440 |

The previous exact authorization gate matched zero of the supplied extracted
placements. PF4582 had 199 evidence-authorized records but no exact placement
matches for this input corpus. That result describes these hashed offline inputs;
it is not a claim that production previously had zero visible NPCs.

All 24,942 newly eligible placements are conservatively classified
`GAMEPLAY_INCOMPLETE_BUT_SAFE`: they meet structural/non-placeholder runtime
checks, but have not passed official-client gameplay acceptance. “Safe” here does
not establish combat, weapon balance, quest coverage or full NPC behavior. Private
template data still has incomplete gameplay definitions. This large activation
change requires a separate staging/official-client acceptance before production.

## Validation receipt

Pre-commit regression: Legacy/NewEngine build PASS; NewEngine 739/739 PASS;
AOtomation 1,128/1,128 PASS;
content guard PASS over 716 current runtime source files; all six guard mutation
cases PASS. DAO architecture guard PASS with zero new violations (three existing
Legacy baseline exceptions), character DAO 551 checks PASS with disposable cleanup,
and mission DAO 275 isolated production-source checks PASS. No database/domain
implementation or schema source files were changed.

The semantic inventory records 83 content-bearing files migrated, 74 content
bridges removed, five Accepted content catalogs removed, 21 content files
physically deleted and 32 Legacy content files unlinked from NewEngine. Three
unlinked generic DTO dependencies are recorded separately. All semantic candidates
have a disposition; current runtime content violations are zero. The baseline's
444 syntax candidates are not presented as 444 independent semantic violations.

The canonical generated-combat coordinator regenerated the existing evidence
cohort successfully without historical raw dependencies. Its generation identity
is `9e8e126f583183e3598a114612f8fbef09e8a29e927948447cb341d15d492374`.

The final exact-source receipt and remaining risks are recorded below once the
Windows and private-platform gates complete. Intermediate failed runs are retained
in local task logs and are not accepted release evidence.
