# ZoneEngine_New accepted NPC activation inventory

Evidence-only source projection. This is not a runtime catalog or a claim of exhaustive actor parity. Reproduce with `powershell -NoProfile -File Tools/accepted_npc_activation_inventory.ps1`; use `-Validate` to compare current outputs without writing and `-SelfTest` to test the lexical source projector.

## Scope and decision

The compiled Legacy registration/dispatcher/direct-creation-call surfaces are enumerated. Exact actor expansion is incomplete for the explicitly listed dynamic/database/transitive sources. That is a concrete inventory blocker, not evidence that accepted NPCs may be dropped. The current New adapters do not cover the complete Legacy activation surface.

Unfinished consumer analysis uses assessmentStatus=CONSUMER_EXPANSION_PENDING with currentNewEngineStatus=null, outside the completed accepted-mapping ledger. It is not DATA_INCOMPLETE: that label requires an actually proven missing authoritative field. The whole exhaustive inventory remains INCOMPLETE until the pending consumer expansion is performed.

- registeredModules: 19
- dispatcherCalls: 28
- entrySurfaces: 32
- staticNamedInitializerRecords: 1338
- combatBindings: 1551
- combatActors: 1565
- combatReadyBindings: 545
- explicitNewSocialAdapters: 22
- commercialNpcShopCapabilities: 19
- standaloneAcceptedWorldShops: 3
- generatedSelectableBundles: 5
- rawOfficialPlacementRecords: 32805
- acceptedOfficialPlacementRecords: 199
- compiledDirectCreationCalls: 34

Do not sum these overlapping views. Combat certification is independent of placement/social/vendor activation. In particular, Legacy unresolved combat contracts can retain passive visible actors. Raw placement records are not all accepted actors.

The eight initially unmapped creation calls are reconciled: four GM Spawn commands and CombatTestMobArchetype are explicitly excluded from ambient accepted population; OrdinaryEnemyRuntimeService is the existing ordinary factory; PetRuntimeService is an owner/nano-generated factory; ThrakGardenKeySilvertailTransform is an accepted quest-triggered actor transform. The latter three are now separate source surfaces, not missing source identities fabricated from their runtime instance counts.

## Registered modules

| Module | NPC dispatcher | Exact source |
| --- | --- | --- |
| AreteContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/AreteContentModule.cs:1 |
| MontroyalContentModule | False | AORebirth/Server/ZoneEngine/Core/Playfields/Content/MontroyalContentModule.cs:1 |
| SubwayContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/SubwayContentModule.cs:1 |
| TempleOfThreeWindsContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/TempleOfThreeWindsContentModule.cs:1 |
| JobePlatformContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/JobePlatformContentModule.cs:1 |
| NascenceCoreContentModule | False | AORebirth/Server/ZoneEngine/Core/Playfields/Content/NascenceCoreContentModule.cs:1 |
| NascenceLifeContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/NascenceLifeContentModule.cs:1 |
| NascenceDungeon1ContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/NascenceDungeon1ContentModule.cs:1 |
| NascenceDungeon2ContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/NascenceDungeon2ContentModule.cs:1 |
| NascenceDungeon3ContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/NascenceDungeon3ContentModule.cs:1 |
| NascenceDungeon4ContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/NascenceDungeon4ContentModule.cs:1 |
| ThrakOmniGardenContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/ThrakOmniGardenContentModule.cs:1 |
| DojaResearchContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/DojaResearchContentModule.cs:1 |
| RomeBlueCityContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/RomeBlueCityContentModule.cs:1 |
| AndromedaIccHqContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/AndromedaIccHqContentModule.cs:1 |
| IccShuttleportContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/IccShuttleportContentModule.cs:1 |
| HoloDeckContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/HoloDeckContentModule.cs:1 |
| MissionInstanceContentModule | True | AORebirth/Server/ZoneEngine/Core/Playfields/Content/MissionInstanceContentModule.cs:1 |
| PrivateCityContentModule | False | AORebirth/Server/ZoneEngine/Core/Playfields/Content/PrivateCityContentModule.cs:1 |

## Accepted NPC entry surfaces

| Surface | Extracted named source records (not live count) | Authority |
| --- | ---: | --- |
| AbanGardenSpawn | 8 | AORebirth/Server/ZoneEngine/Core/Playfields/AbanGardenSpawn.cs |
| AlexAreaMobRuntime | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/AlexAreaMobRuntime.cs |
| AndromedaIccHqSpawn | 54 | AORebirth/Server/ZoneEngine/Core/Playfields/AndromedaIccHqSpawn.cs |
| AreteFinishCaptureMobRuntime | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/AreteFinishCaptureMobRuntime.cs |
| AreteLandingSpawn | 90 | AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs |
| CapturedAreteRobotSpawnOrchestrator | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotSpawnOrchestrator.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotContentProvider.cs |
| CapturedSubwayEncounterRuntimeService | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayEncounterRuntimeService.cs |
| CapturedSubwayVendorRuntimeService | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayVendorRuntimeService.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/CapturedSubwayVendorContentProvider.cs |
| CapturedTempleOfThreeWindsEncounterRuntimeService | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsEncounterRuntimeService.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/CapturedTempleOfThreeWindsEncounterRules.cs |
| HoloDeckSpawn | 8 | AORebirth/Server/ZoneEngine/Core/Playfields/HoloDeckSpawn.cs |
| IccShuttleportSpawn | 35 | AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportProfilePopulationCatalog.g.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/IccShuttleportSpawn.cs |
| JunkyardCleaningRobotRuntime | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/JunkyardCleaningRobotRuntime.cs |
| LoreleiOasisMobRuntime | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/LoreleiOasisMobRuntime.cs |
| MarcusPadAmbientCombat | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/MarcusPadAmbientCombat.cs |
| MissionAcgOperationalRuntime | 0 | AORebirth/Server/ZoneEngine/Core/Missions/MissionAcgOperationalRuntime.cs |
| MissionInstanceSpawn | 1 | AORebirth/Server/ZoneEngine/Core/Playfields/MissionInstanceSpawn.cs |
| NascenceCoreHecklerSpawnOrchestrator | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/NascenceCoreHecklerSpawnOrchestrator.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceCoreHecklerContentProvider.cs |
| NascenceDungeon1Spawn | 48 | AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon1Spawn.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon1SpawnData.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon1Rules.cs |
| NascenceDungeon2Spawn | 38 | AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon2Spawn.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon2SpawnData.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon2Rules.cs |
| NascenceDungeon3Spawn | 69 | AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon3Spawn.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon3SpawnData.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon3Rules.cs |
| NascenceDungeon4Spawn | 87 | AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon4Spawn.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon4SpawnData.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon4Rules.cs |
| NascenceLifeSpawn | 868 | AORebirth/Server/ZoneEngine/Core/Playfields/NascenceLifeSpawn.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon1Rules.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon2Rules.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon3Rules.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/NascenceDungeon4Rules.cs |
| OrdinaryEnemyRuntimeService | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyRuntimeService.cs<br>AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyProfile.cs |
| PerkResetServiceProviderSpawn | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/PerkResetServiceProviderSpawn.cs |
| PetRuntimeService | 0 | AORebirth/Server/ZoneEngine/Core/PetRuntimeService.cs<br>AORebirth/Server/ZoneEngine/Core/PetCombatRules.cs |
| PlayfieldDbMobSpawnRuntimeService | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldDbMobSpawnRuntimeService.cs |
| RomeBlueCitySpawn | 22 | AORebirth/Server/ZoneEngine/Core/Playfields/RomeBlueCitySpawn.cs |
| ScarlettDalquistSpawn | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/ScarlettDalquistSpawn.cs<br>AORebirth/Server/ZoneEngine/Core/Doja/DojaChipInteractionRules.cs |
| SurveillanceDroidRuntime | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/SurveillanceDroidRuntime.cs |
| ThrakGardenKeySilvertailTransform | 0 | AORebirth/Server/ZoneEngine/Core/Thrak/Quests/ThrakGardenKeySilvertailTransform.cs<br>AORebirth/Server/ZoneEngine/Core/Thrak/Quests/ThrakGardenKeyInteractionRules.cs |
| ThrakOmniGardenSpawn | 10 | AORebirth/Server/ZoneEngine/Core/Playfields/ThrakOmniGardenSpawn.cs |
| WorldPopulationController | 0 | AORebirth/Server/ZoneEngine/Core/Playfields/WorldPopulationController.cs |

Every surface retains exact source hashes and fieldSourceLines references for identity, PF, template, level, stats, appearance and capability expressions in the JSON. Source rows preserve exact initializer expressions. A missing field remains missing; names/positions/comments do not manufacture original IDs. Source-reference line arrays avoid duplicating entire runtime source files into the evidence artifact.

If a combat-manifest row has no configured source identity while its exact Legacy source/PF has a connected New catalog, its cross-view join remains pending with null status. For example, the Thrak combat view omits IDs even though the garden provider supplies exact vendor IDs. Name matching is not used to manufacture that missing manifest join; the separate New catalog connections retain their real identities.

## Existing combat-binding view

| Surface | Bindings | Actors | Combat-ready bindings |
| --- | ---: | ---: | ---: |
| arete-additional-captured-actors | 17 | 17 | 4 |
| arete-family | 83 | 96 | 39 |
| nascence-core-hecklers | 40 | 40 | 0 |
| nascence-life | 868 | 868 | 0 |
| rome-blue-city | 22 | 22 | 0 |
| subway-initial-encounters | 3 | 3 | 0 |
| subway-merchants | 6 | 6 | 0 |
| subway-ordinary | 322 | 322 | 322 |
| temple-named-encounters | 12 | 12 | 12 |
| temple-ordinary | 167 | 167 | 167 |
| temple-reanimated-corpse-adds | 1 | 2 | 1 |
| thrak-omni-garden | 10 | 10 | 0 |

## Explicit New connections

- ACTIVE: PF7010 SimpleChar:7A18B924, legacy:ScarlettDalquistSpawn:7010:7A18B924; commercial shop capability=False; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedSocialNpcCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF127 SimpleChar:79135F51, Tailor; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedSubwayMerchantCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF127 SimpleChar:79135F52, Basic Quality Weaponsdealer; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedSubwayMerchantCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF127 SimpleChar:79135F53, Basic Quality Armorer; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedSubwayMerchantCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF127 SimpleChar:79135F54, Basic Quality Pharmacist; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedSubwayMerchantCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF127 SimpleChar:79135F55, Basic Tools Merchant; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedSubwayMerchantCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF127 SimpleChar:79135F56, Container Supplier; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedSubwayMerchantCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF6553 SimpleChar:78E0FC65, CreateStan; commercial shop capability=False; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedAreteQuestNpcCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF6553 SimpleChar:78E0FC69, CreateSarah; commercial shop capability=False; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedAreteQuestNpcCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF6553 SimpleChar:78E0FC81, CapturedAreteMarcoSpidaVendorContentProvider; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedAreteVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF6553 SimpleChar:78E0FC6B, CapturedAreteLoreleiVendorContentProvider; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedAreteVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4677 SimpleChar:79758F3F, Thrak:0x79758F3F; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4677 SimpleChar:79758F3E, Thrak:0x79758F3E; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4677 SimpleChar:79758F3B, Thrak:0x79758F3B; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4677 SimpleChar:79758F3C, Thrak:0x79758F3C; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4677 SimpleChar:79758F3D, Thrak:0x79758F3D; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4676 SimpleChar:7A2013B7, Aban:0x7A2013B7; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4676 SimpleChar:7A2013B4, Aban:0x7A2013B4; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4676 SimpleChar:7A2013B5, Aban:0x7A2013B5; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4676 SimpleChar:7A2013B8, Aban:0x7A2013B8; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4676 SimpleChar:7A2013B6, Aban:0x7A2013B6; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
- ACTIVE: PF4676 SimpleChar:7A2013B9, Aban:0x7A2013B9; commercial shop capability=True; AORebirth/Server/ZoneEngine_New/Core/Mobs/AcceptedGardenVendorCatalog.cs. Declared shops still require complete actual item data.
Quest hand-ins do not imply commercial vendor capability. Commercial NPC shop counts derive from the actual AcceptedNpcBinding final argument and explicit provider stock contract, not actor names or quest interaction availability.
- ACTIVE standalone world shop (not an NPC): Junk Shop, source vendor 317157897; AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteAlexAreaVendorContentProvider.cs:17.
- ACTIVE standalone world shop (not an NPC): ICC Ammunition, source vendor 317157893; AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteAlexAreaVendorContentProvider.cs:37.
- ACTIVE standalone world shop (not an NPC): ICC Tech Supplies, source vendor 317157896; AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteAlexAreaVendorContentProvider.cs:63.
- ACTIVE: five accepted generated mission bundle routes, via GeneratedMissionWorld and GeneratedMissionNpcFactory; dynamic persisted instance counts remain separate.

## Official placement layer

The current artifacts contain 199 records with RuntimeActivationAuthorized=true, IdentityResolved=true, BehaviorReady=true and a nonempty ExistingAoRebirthProfile, out of 32805 raw records. These are separate placement/hash evidence for existing profiles, not extra unique actors. Missing exact New hash/template evidence does not revoke the accepted Legacy profile; the JSON preserves each exact record and its missing bridge fields.

## Remaining blockers and exclusions

- ACTOR_CENSUS_NOT_EXHAUSTIVE: compiled module/dispatcher and selected direct creation methods in ZoneEngine.csproj are enumerated, but DB rows, dynamically compiled scripts, owner-generated pets, quest transforms and transitive content factories are not all expanded into deduplicated exact actor rows.
- STATIC_SOURCE_RECORDS_OVERLAP_COMBAT_BINDINGS: the source initializer table and combat binding table are alternative views; never sum them or infer identity from matching names/positions.
- NEW_BINDING_GAP: accepted Legacy profiles without exact New factory/placement/behavior adapters remain ACCEPTED_BUT_NOT_CONNECTED; combat resolver presence alone is not an activation bridge.
- DATABASE_ACTOR_ROWS_NOT_INSPECTED: live/deployed DB state is out of scope; compiled MobSpawnDao path is recorded, not queried.
- RAW_PLACEMENTS_NOT_ACCEPTED_CONTENT: only the explicit four-field accepted authorization predicate enters the separate official layer; other raw records are UNPROVEN, not automatically missing gameplay.
- DYNAMIC_INSTANCE_COUNTS_NOT_STATIC: generated five-bundle mission objects and conditional authored/escort spawns cannot be added to global static actor counts.

## Validation boundary

The coordinating task recorded **420/420 focused tests PASS** for the final pre-commit gap-closure checkpoint, including exact commercial capability and real packaged stock/pricing checks. This is attributed historical test evidence, not a test executed by this generator or a PASS automatically conferred on future source changes.

Generator validation proves reproducibility, source presence and declared registration scope only. No build, live session, client, capture, migration or database inspection is performed. Full runtime readiness remains the root acceptance decision; this evidence explicitly cannot certify exhaustive actor parity.
