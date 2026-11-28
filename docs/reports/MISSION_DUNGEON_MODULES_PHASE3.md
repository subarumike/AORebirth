# Mission Phase 3 modular dungeon architecture

## Source and scope

| Input | Revision |
| --- | --- |
| Starting public master | `98090d8e1b6411bf8d413a0cab683f922a515a84` |
| Validated implementation | `0237738807a6ce46bbc8e7d68a1b789443ebcc5e` |
| Feature branch | `codex/mission-dungeon-modules` |

GitHub independently read back the implementation SHA on the feature branch. Public master remained at the starting SHA. The feature ancestry contains no merge of reward-durability work.

The prior accepted layout stored a generator version, seed, layout plan and objective binding. The plan did not identify the room resources, door styles, district population or collision inputs used to materialize it. This allowed reconstruction to read changed assets from the active GameData tree.

## Runtime ownership

| Contract | Responsibility |
| --- | --- |
| `IMissionDungeonGenerator` | `DungeonLayoutGenerator` resolves the retained revision, validates the selected profile, invokes the existing procedural generator and binds original AO geometry. It supplies shared verified collision, room shape, district and population inputs. |
| `IMissionDungeonPopulationModule` | `MissionDungeonPopulationModule` reserves the exact objective NPC and registers ordinary district spawns with `HashSpawnSystem`. It uses retained templates and stable branch selections and initial transforms for content-bound missions. |
| `IMissionDungeonObjectivePlacement` | `DungeonObjectivePlacement` selects an original district position in a connected room and validates the player approach on the existing collision and route checks. On reconstruction it verifies the exact persisted point before the world becomes available. |

`QuestDungeonService` still owns the acceptance and admission sequence, including the existing persistence gate, key grant and atomic claim. `QuestDungeonPlayfield` consumes the shared layout, delegates population and placement validation, builds existing doors and floor transitions, and restores the chest population. The Phase 2 objective completion and reward paths remain authoritative.

## Content and compatibility

`DungeonGenerationContent` and `DungeonContentIndex` use the Phase 1 snapshot provider. New accepted records keep generator version 3 and add a schema-versioned content reference to the existing accepted quest JSON. The referenced archive under `Missions/DungeonContent` retains exact original room metadata and occupancy, ground and surface data, district spawn points, entrance and floor-transition roles, door and generation definitions, player collision metadata and a shared, digest-addressed NPC template and spawn-feature catalog. Runtime resolution verifies the bytes and caches parsed resources by content identity. Missing or changed retained assets fail the reconstruction with a revision-specific error.

Historical V2 seeds retain their original policy gate and golden outputs. Historical V3 rows without a content reference keep their supported current-root compatibility behavior. Historical V1 live restore still rejects the missing verified original binding. A malformed active revision index blocks new acceptance while older retained and supported historical layouts remain independently resolvable.

The private staging tool is `Tools/stage_mission_dungeon_content.py`. Its output is a distinct candidate GameData tree. It accepts the earlier approved GameData through `--retain-from-gamedata` to preserve old bundles and shared catalogs. Release admission recognizes the exact runtime-consumed bundle paths. A real isolated staging run produced 47 admitted files for four original dungeon styles with one shared NPC catalog. Canonical GameData and the private remote were unchanged. A future master integration requires approved private publication of the bundle and an exact-input Windows release.

## Persistence and limits

The existing `ICharacterQuestStore`, `IMissionDao` and transaction boundaries retain authority for accepted definitions, keys, objective progression, chest seals and token census. This feature adds no SQL or database migration. Sealed chest identities, locations and remaining items continue to come from the existing store. The generated chest loot rules and retail packet serializers are unchanged.

The population module uses durable killed slots to suppress spawns. Reconstruction fails closed if the kill count and markers disagree or a kill commits between census read and seal. The original objective identity stays fixed. Historical records without a token census retain their existing instance-scoped behavior.

Original doorway placement and initial lock rolls are reproducible. Later lockpick, opening and closing effects remain scoped to the live world. Persisting those changes would alter established door behavior and was deferred pending a verified contract. Retained NPC templates cover spawn selection and features. Generic combat tuning and the shared item-template catalog retain their existing runtime ownership.

## Validation results

| Check | Observed result |
| --- | --- |
| Governed Windows feature regression | 296 passed of 297 executed. One byte-identical public-master exception, zero skips and zero new failures. Full run 112.36 s. |
| Existing deterministic mission responses | All 78 reproduced SHA-256 `3d1aa7e35baa65d7669390c439d75311e12d249385c9e9e2eb304ea2fe834604`. |
| All five retained objective lifecycles | 5/5 accepted under A and completed after reconstructing A with revision B active. The test store checks exact layout, accepted binding, chest packet bytes, single reward, item ownership and key retirement. |
| Physical placement | Real style 324 single-floor and style 341 two-floor layouts retained the exact original reachable point on reconstruction. The float boundary regression accepts the authored step and rejects the next representable height above it. Missing collision rejects placement. |
| Historical layout and content | Three original V2 golden results, 288 configured profile cases, V1 refusal, historical V3 restoration, solo and multi-floor archive replay, and missing and tampered retained inputs were covered. |
| Population | Stable surviving branch transforms, no respawn, stale-census rejection, consistent durable kill flags and exact revision-specific NPC materialization passed. |
| Disposable MySQL | 98 acceptance checks and 50 chest checks passed through the existing DAO validation entrypoints using linked production sources. |
| Private staging and admission | 3/3 tests passed for declared package paths, carrying older bytes forward and rejection of a damaged prior bundle. Two actual staged revisions used the exact original geometry files. |
| Architecture | 31/31 mutation groups passed. Zero introduced findings, six resolved baseline findings and 216 inherited findings remain open. The guard checked authoritative GameData. |
| Hosted source check | Passed on the validated implementation SHA. [Runtime data architecture check](https://github.com/subarumike/AORebirth/actions/runs/46125305341). |

The public baseline exception is `MissionChestProtocolTests.OrdinaryRetailChestUsePublishesContentsBeforeSuccessAndCloseBeforeSuccess`. Both revisions differ at byte index 16 with expected 21 and actual 20. The baseline rerun used tree `b08264e967115ad7c843072b34317c698220e754`, verified byte-identical to public master `98090d8e1b6411bf8d413a0cab683f922a515a84`. Both packet assertion inputs and the chest interaction source are unchanged in this feature.

The same run preserved the other chest protocol fixtures, including captured spawn, lockpick and transfer acknowledgement fixtures. Original-client gameplay acceptance remains unverified. The Windows runs were isolated development validation. A release, service restart, or production database change was outside this task.

| Validation evidence | SHA-256 |
| --- | --- |
| Full regression TRX | `363d7e34af2832768c9c4868b516d6fc37b323e31941e05b95c59158ba3aae11` |
| Governed regression receipt | `5a3b5d37098132ffdf1dc119f40782893068bf9d146aa6fdbe52b3d8ca6f2ef9` |
| MySQL acceptance receipt | `e8427a8448c83611af8b003cba37613015d4fe4e9aef15a15f62fe48460cdc64` |
| MySQL chest receipt | `3a743464728ca462e565abce79537ec29b8df31d4652b5e2acf7c35203a6abce` |

Receipts and private archive bytes stay in the ignored feature-validation worktree. The published branch contains source, tests, tooling and this report.

## Performance

The following equivalent-selection measurements compare three warmed iterations that build original collision geometry on each selection with selection over geometry that the dungeon build already owns.

| Original layout | Rebuild each time, wall / CPU / allocated bytes | Shared geometry, wall / CPU / allocated bytes |
| --- | --- | --- |
| Style 324, one floor, seed 818302858 | 20.00 ms / 15.62 ms / 16,863,744 | 0.04 ms / 0 sampled ms / 36,976 |
| Style 341, two floors, seed 34719395 | 17.78 ms / 15.62 ms / 19,237,400 | 0.02 ms / 0 sampled ms / 43,696 |

The added cold physical placement check incurred 310.57 ms wall and 296.88 ms sampled CPU with 42,024,912 bytes allocated in the single-floor case, and 357.19 ms wall and 359.38 ms sampled CPU with 41,192,008 bytes allocated in the two-floor case. CPU samples use process resolution, so a displayed zero is below measurement resolution. Geometry reuse removes redundant construction work, while the stricter physical check adds a measured cost to new acceptance and reconstruction. Whole-dungeon or live throughput improvement remains unverified.

The fresh disposable acceptance benchmark measured one offer lookup and six claim commands. The inactive full-read comparison returned 2,515–2,550 offers and the active indexed lookup returned one. This confirms the existing Phase 2 DAO behavior and does not attribute that query improvement to Phase 3. Census reconciliation compares values already read by the existing seal and adds no polling or SQL during movement.

## Status

| Required status | Result |
| --- | --- |
| `DUNGEON_GENERATION_MODULE` | PASS |
| `NPC_POPULATION_MODULE` | PASS |
| `OBJECTIVE_PLACEMENT_MODULE` | PASS |
| `VERSIONED_DUNGEON_CONTENT` | PASS |
| `DUNGEON_RECONSTRUCTION` | PASS for verified retained and supported historical layouts |
| `ORIGINAL_AO_GEOMETRY` | PASS |
| `PERSISTENT_NPC_STATE` | PASS for the existing durable census contract |
| `PERSISTENT_CHEST_STATE` | PASS |
| `PERSISTENT_DOOR_STATE` | PARTIAL |
| `FIVE_MISSION_OBJECTIVES` | PASS |
| `HISTORICAL_DUNGEON_COMPATIBILITY` | PASS for supported V2 and V3, with the explicit V1 refusal |
| `EXISTING_DAO_PRESERVED` | YES |
| `GAMEDATA_AUTHORITY` | PASS |
| `DETERMINISTIC_MISSION_PARITY` | PASS |
| `DOA_ENFORCEMENT` | PASS |
| `PERFORMANCE_REGRESSION` | YES in the measured cold physical validation path |
| `MASTER_INTEGRATION_READY` | NO pending approved private content publication and review of cold construction cost |
| `PRODUCTION_DEPLOYMENT` | NOT_PERFORMED |

The ordinary NPC's durable identity is its spawn slot and branch. The entity's transient AO network instance is allocated by the existing spawn service. Only the accepted objective NPC's exact persisted AO identity is reused. Quantifying full construction and login latency under concurrent load and original-client verification remain separate acceptance work.

## Changed source and validation paths

- `AORebirth/Libraries/Source/AORebirth.DungeonGenerator/DungeonEntranceMap.cs`
- `AORebirth/Libraries/Source/AORebirth.DungeonGenerator/DungeonGenerator.cs`
- `AORebirth/Libraries/Source/AORebirth.World.Collision/DungeonCollisionBuilder.cs`
- `AORebirth/Libraries/Source/AORebirth.World.Collision/DungeonStyleCatalog.cs`
- `AORebirth/Server/ZoneEngine_New.Tests/MissionChestObjectiveIntegrationTests.cs`
- `AORebirth/Server/ZoneEngine_New.Tests/MissionDungeonObjectivePlacementTests.cs`
- `AORebirth/Server/ZoneEngine_New.Tests/MissionDungeonPopulationModuleTests.cs`
- `AORebirth/Server/ZoneEngine_New.Tests/ProceduralDungeonFoundationTests.cs`
- `AORebirth/Server/ZoneEngine_New.Tests/QuestDungeonDestinationTests.cs`
- `AORebirth/Server/ZoneEngine_New.Tests/RetainedDungeonContentTests.cs`
- `AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj`
- `AORebirth/Server/ZoneEngine_New/Core/GameData/DungeonGenerationContent.cs`
- `AORebirth/Server/ZoneEngine_New/Core/GameData/GameDataContentProvider.cs`
- `AORebirth/Server/ZoneEngine_New/Core/GameData/NpcTemplateCatalog.cs`
- `AORebirth/Server/ZoneEngine_New/Core/GameData/RetainedDungeonContent.cs`
- `AORebirth/Server/ZoneEngine_New/Core/GameData/RetainedMissionNpcContent.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Missions/Content/MissionContent.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Mobs/MonsterCombatRules.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Playfield/HashSpawnSystem.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Playfield/PlayfieldManager.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Playfield/QuestDungeonPlayfield.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/DungeonLayoutGenerator.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/DungeonObjectivePlacement.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/DungeonPopulationPolicy.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/IMissionDungeonGenerator.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/MissionChestPlacement.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/MissionDungeonPopulationModule.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/MissionTokenProgress.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/QuestDungeonParameters.cs`
- `AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/QuestDungeonService.cs`
- `AORebirth/Server/ZoneEngine_New/Program.cs`
- `Tools/NewEngineRuntimeContentGuard/DoaModuleBoundary.cs`
- `Tools/NewEngineRuntimeContentGuard/DoaMutationTests.cs`
- `Tools/stage_mission_dungeon_content.py`
- `Tools/tests/test_stage_mission_dungeon_content.py`
- `tools/windows_release_contract.py`
