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

### Exact Windows source

Accepted public source: `e7a306c566c853ae72a7e9e55a39b889b2a6902f`.
`Tools/accept_windows_source.cmd --expected-sha <that SHA> --mandatory-gate`
completed with `WINDOWS_ACCEPTANCE=PASS` and all 12 mandatory stages PASS.
The receipt is `build-verify/windows-acceptance-e7a306c5.env`; the retained log is
`tools-temp/content-cleanup-001/windows-exact-e7a306c5.log`.

The accepted run includes NewEngine 739/739 and AOtomation 1,128/1,128 tests.
The accepted NewEngine DLL SHA-256 is
`6850d6afbf5de1624897a62780d73f4e067f8dcf0dfeba0fe5ab9a4fb89db8e1`.
The final placement-impact evaluation used that DLL; counts are unchanged from
the candidate evaluation. All 716 source hashes in the semantic audit match the
architecture guard's accepted source inventory.

The earlier exact-source attempt on `590c7d03` failed because an engine-management
test still required the removed compiled-content package path. Commit `e7a306c5`
replaced that stale assertion with checks for the content guard and its failure
handling. The full acceptance was rerun successfully. Intermediate failed runs
remain in local task logs and are not accepted release evidence.

### Scope of automated acceptance

Synthetic connected tests exercise login admission, inventory/equipment actions,
zoning, reconnects, process restart and durable-state comparisons. They do not
establish official-client rendering, combat balance or complete NPC behavior.
Same-binary content tests cover changed placement/rotation/stats/appearance,
vendor stock, quest rewards, dialogue visibility, summons, weapon variants and
nano bindings. The activation-impact evaluator additionally checks actual spawn
registration for all 630 supplied playfields, including PF4582, PF4544 and PF800.

No schema or player DAO model change is included. Disposable database test
fixtures are isolated from production. Executable-only Legacy rollback after
NewEngine writes remains unsupported; this cleanup does not alter that boundary.

### Files inspected and changed

The semantic audit lists every inspected runtime source path and hash, plus the
baseline classification and review disposition. The bridge inventory lists
consumers, replacement content and retained mechanics for each migrated group.
Additional inspection covered the named content JSON, regression fixtures,
Windows build/acceptance contracts, disposable acceptance harness, generated
combat coordinator outputs and private publication/acceptance adapters.

Public changes relative to the starting source are 69 added paths, 106 modified
paths and 23 removed paths (198 total, using Git's no-renames count). This is
separate from the 21 physically removed runtime-content paths and 32 unlinked
Legacy content sources. Renames are counted as one removal plus one addition.
The exact changed-file inventory is available with
`git diff --name-status --no-renames 323652db3b07d9640dca5b084a3cfd807baa6b0c HEAD`.

The five requested report artifacts are accompanied by refreshed NewEngine
dependency/persistence inventories and the canonical combat coverage/manifest.
The canonical regeneration changes provenance identities, not gameplay rows,
catalog definitions, fixtures, formulas or coverage counts. Private build code
and operator payloads were not added to public Git.

### Exact connected and disposable acceptance

Schema/DAO/restart acceptance and connected LoginEngine/NewEngine acceptance
both PASS against a frozen copy of the accepted `e7a306c5` Windows outputs.
All 10,484 frozen files matched the accepted build before the gates. Afterward,
all 10,385 binary, dependency, content and configuration inputs were unchanged;
test-generated output files are excluded from that second count.

LoginEngine SHA-256:
`b8ea28d1322b17ec4f5ea9f3593c15ef71c0296a9a93c6a1665d4963e3584efd`.
NewEngine SHA-256 is the accepted `6850d6af...` hash recorded above.
The final receipt is
`tools-temp/content-cleanup-001/frozen-acceptance-results.json`.
Schema log SHA-256:
`fa921485bbd8d9d4f407e31f8da8cc86df550149912e154d68dc1fd4e7e16429`.
Connected log SHA-256:
`2dab506c228ccd06a7de2b4cc4b2f7e24e8c0b477a2273b8f0b8ba88ceb2103e`.
Production contact was NO, official client exercised was NO, and disposable
container/network residue was NONE. Earlier dirty-candidate results were not
reused as evidence for the final committed source.

### Exact private-platform acceptance

Native exact-source acceptance PASS. The private assembled source is
`44a41e9a6871d154ffd4a2b6db33f7df4d0de2f1`, combining accepted public source
`e7a306c566c853ae72a7e9e55a39b889b2a6902f` with private operations source
`f91a762acc02f711f54843d00cb5349c946001a8`. Both private commits were pushed only
to the verified private repository.

The native solution build, NewEngine 739/739 tests, publication/ELF/startup checks,
separate Legacy publication and placement checks, and release package/transaction
acceptance all PASS. The run also checked exact source identity, source cleanliness,
default-engine contracts, source inventory and the content architecture guard.
The release regressions include 81 deployment and 21 package tests, all PASS.
The native acceptance log is
`tools-temp/content-cleanup-001/native-acceptance-e7a306c5-retry.log`.

The candidate content manifest covers 4,782 files and 273,309,543 bytes, SHA-256
`63835b38bbd289102f6a165e0264cd27f4a42bda3fe60347422af823bf1b2480`.
Runtime templates and world definitions are the accepted repository content;
extracted geography is supplied by the private playfield archive. The separate
operator template catalog used for the 24,942-placement impact analysis was not
injected into this package. Therefore that impact count is not a claim about the
packaged population. No private payload or private build implementation was
committed to the public repository.

The first native attempt encountered a historical checkout baked into the SDK
image. The runner now requires a fresh workspace named for the exact assembled
SHA. That repair is included in the private operations input above; the full
acceptance was rerun successfully in the fresh workspace. No production state
was involved.

The exported private artifact archive is 395,973,001 bytes, SHA-256
`eca35ad5df3b3ec7e21c2008cca4b393cc99a48c107662f2b3672a4f79507e51`.
Its NewEngine DLL SHA-256 is
`e9a3672242f564e54af93c07118e72b8d1ecadddf5ece5d88e938c158fae3272`.
The extracted playfield archive input contains 4,710 files and 264,151,897 bytes,
SHA-256 `6b3c06c234923d07a8d476b710d650e620aafd0775602c2e38160af8bdc69b26`.
The private archive and extracted artifact are retained locally; no deployment
or public artifact upload was performed.

An additional read-only MSBuild evaluation of the actual native Release/linux-x64
graph found the same 716 source paths across 10 projects: no extra, missing or
unresolved inputs. Semantic review covered 13 normalized private compatibility
diffs and 16 conditional-source files, finding zero runtime-content violations.
Another 224 raw hash differences were encoding or line endings only. Private
implementation details remain in the locally retained native review evidence;
the public audit contains aggregate results only. This declaration evaluation
excludes target-generated compiler metadata and package binary implementations.

### Final documentation commit

The commit containing this final receipt only updates the report, final impact
binary hash, native audit summary, current task and project state. The tested runtime source remains
`e7a306c566c853ae72a7e9e55a39b889b2a6902f`; a later documentation commit is not
represented as a different tested binary. Both implementation commits and the
final receipt commit are pushed to `codex/data-driven-world-20260915`.

## Content ownership decision table

| Gate | Result |
| --- | --- |
| Repository-wide runtime content audit completed | YES, discovered source graph with documented static-analysis limits |
| Scarlett removed from compiled runtime content | YES |
| Accepted NPC catalogs removed as content authorities | YES |
| Arete-specific content moved to data | YES |
| Garden vendor content moved to data | YES |
| Subway merchant content moved to data | YES |
| Buckethead-specific content moved to data | YES |
| Mission NPC content separated from generation mechanics | YES |
| NPC weapon assignments are data-driven | YES |
| Quest content is data-driven | YES |
| Vendor content is data-driven | YES |
| Dialogue content is data-driven | YES |
| Runtime evidence allowlist removed | YES |
| Editable playfield data is runtime spawn authority | YES |
| Evidence provenance preserved separately | YES |
| `AAAA` cannot become attackable fallback content | YES |
| PF4544 can use valid editable spawn data | YES |
| Content edits work without recompiling server | YES |
| New runtime content hardcoding introduced | NO |
| New direct SQL introduced | NO |
| Player DAO persistence changed | NO |
| Production modified | NO |

## Remaining risks

- Staging and official-client acceptance are still required before deployment.
  In particular, 24,942 newly eligible placements have no new gameplay-acceptance
  claim; 7,088 unresolved and 127 malformed source rows remain skipped.
- Template/family stats, weapon balance, unsupported mechanics and missing
  behaviors remain content/gameplay work. Removing the authorization gate does
  not complete those definitions.
- Content changes require a validated catalog reload or candidate restart.
  There is no new remote hot-reload endpoint.
- The guard detects the documented source patterns and must accompany code
  review. It is not a general proof that future arbitrary C# cannot encode data.
- Legacy is retained. This task neither removes Legacy nor performs a full DAO
  conversion. Existing durable-state and rollback requirements remain intact.
