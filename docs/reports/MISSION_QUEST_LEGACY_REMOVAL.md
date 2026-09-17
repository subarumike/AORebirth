# Mission/quest removal: dependency and behavior baseline

Starting SHA: `ead34a4a55eb01df47f1afd8c46ca3cfef13254a`.
Actual retained source count: **35** (not inferred from the request).
The dependency classifications below were established by source inspection before each assigned group was edited.
The untouched starting NewEngine suite ran first: **537 passed / 20 failed**; exact results are in `baseline-tests.log`.

All retained paths below are under `AORebirth/Server/ZoneEngine_New/SharedGameplay/Missions/`.
Columns: C=specific game content; G=generic mechanic; P=wire/content/durable schema compatibility;
R=required behavior needs clean replacement; D=delete without replacement of that file's responsibility.
DTO compatibility means schema only, not retained runtime algorithms.

| File | Active consumers | C | G | P | Current native replacement / required responsibility | R | D |
|---|---|---|---|---|---|---|---|
| AreteContentLoadResult.cs | Dialogue content loader; old quest loader/index | NO | NO | NO | Generic read-result contract | YES | NO |
| AreteContentManifest.cs | Manifest loader; InteractionContent; dialogue manifest loading | NO | NO | YES | Editable manifest DTO | YES | NO |
| AreteContentManifestLoader.cs | InteractionContent; dialogue loader | NO | YES | NO | Minimal manifest resolution on existing JSON serializer | YES | NO |
| AreteJsonContentFileLoader.cs | Old quest loader only | NO | YES | NO | Existing native serializer path | NO | YES |
| AreteValidationResult.cs | Native dialogue validation/index; old quest helpers | NO | YES | NO | Generic validation diagnostics | YES | NO |
| IMissionRepository.cs | Old persistent service/coordinator/adapter | NO | NO | YES | Existing IMissionDao and transaction contracts | NO | YES |
| MissionAcgAcceptedQfuBuilder.cs | GeneratedMissionAcgService journal replay | NO | YES | YES | Direct projection from accepted DAO binding/frozen offer | YES | NO |
| MissionAcgBindingRecord.cs | Native materializer | NO | NO | YES | Existing GeneratedMissionBinding | NO | YES |
| MissionAcgIdentityRanges.cs | Materializer; corpse projection/credit policy; login; PlayfieldManager | NO | YES | YES | Preserve identity allocation contract only | YES | NO |
| MissionAcgInstanceBinding.cs | GeneratedMissionAcgService; materializer | NO | YES | YES | Existing GeneratedMissionBinding | NO | YES |
| MissionAcgInstanceState.cs | Old binding wrapper; materializer | NO | YES | NO | Native DAO already owns lifecycle | NO | YES |
| MissionAcgLayoutBundle.cs | Native layout catalog, materializer, world | NO | YES | YES | Compatible editable Layouts.json schema | YES | NO |
| MissionAcgLayoutRecords.cs | Native layout catalog, materializer, world | NO | YES | YES | Compatible layout DTOs; platform binary/hash operations | YES | NO |
| MissionAcgLayoutSelector.cs | GeneratedMissionAcgService acceptance | NO | YES | NO | Filter validated native layout catalog | YES | NO |
| MissionAcgObjectiveBinding.cs | Old QFU construction; inactive old objective contracts | NO | YES | NO | Native GeneratedMissionService/DAO already owns objectives | NO | YES |
| MissionAcgSpatialAuthority.cs | GeneratedMissionWorld envelope | NO | YES | NO | Bounds from loaded layout coordinates | YES | NO |
| MissionDaoRepositoryAdapter.cs | AuthoredQuestService transaction helper | NO | YES | YES | Call existing IMissionDaoTransaction directly | NO | YES |
| MissionDataMapper.cs | Old adapter only | NO | YES | YES | Accepted DAO DTOs directly | NO | YES |
| MissionDiagnostics.cs | Old roll/reward/level helpers | NO | YES | NO | Existing native logger | NO | YES |
| MissionLevelGraph.cs | Old level table | NO | YES | NO | Minimal validated accepted CSV loader | YES | NO |
| MissionLevelGraphData.g.cs | Old level table | YES | NO | NO | Existing accepted MissionLevels.csv | NO | YES |
| MissionLevelTable.cs | Roll service; terminal handler; native token policy | NO | YES | NO | No native loader existed at start; direct CSV runtime required | YES | NO |
| MissionModels.cs | AuthoredQuestCatalog; InteractionContent; authored service and timed turn-in | NO | NO | YES | Keep content schema; use DAO persistence DTOs | YES | NO |
| MissionNpcDifficultyPolicy.cs | GeneratedMissionAcgService initial objects | YES | YES | NO | Editable scaling parameters preserving starting behavior | YES | NO |
| MissionRewardCatalog.cs | Old roll service | YES | YES | NO | Select from existing packaged reward catalogs and explicit policy | YES | NO |
| MissionRewardCoordinator.cs | No current native consumer | NO | YES | YES | Native rewards already transact through DAO | NO | YES |
| MissionRollFeeRules.cs | Mission terminal handler | YES | YES | NO | Fee policy data; native DAO charges atomically | YES | NO |
| MissionRollService.cs | Terminal handler; roll projection; ACG service; old QFU helper | YES | YES | YES | Clean offer assembly using existing native content and wire codec | YES | NO |
| MissionSliderProfile.cs | Handler; projection; roll/evidence/compatibility content helpers | YES | YES | YES | Protocol slider decoding; editable selection policy | YES | NO |
| MissionTypeCatalog.cs | Roll/ACG service; native content type helpers | YES | YES | YES | Persisted enum values and editable icon/action/type metadata | YES | NO |
| PersistentMissionService.cs | AuthoredQuestService and timed turn-in | NO | YES | YES | Minimal authored transitions inside existing caller-owned transaction | YES | NO |
| QuestContentPackLoader.cs | InteractionContent; old quest index | NO | YES | NO | Small native content reader | YES | NO |
| QuestContentPackValidator.cs | Old quest loader | NO | YES | NO | Independent structural/reference validation | YES | NO |
| QuestContentRegistry.cs | InteractionContent; AuthoredQuestCatalog; dialogue validators | NO | YES | NO | Native validated quest index | YES | NO |
| QuestModels.cs | Same consumers; old loader/validator/index | NO | NO | YES | Compatible serialized quest definitions only | YES | NO |

## Starting working behavior

Generated missions: five-offer generation, one-based difficulty-to-QL lookup, signed slider interpretation,
type/selection policy, allocated identities, fee charged with committed offer batch, persisted offers,
owned acceptance and frozen projection, mission keys/artifacts, layout selection/materialization,
private spatial/identity binding, generated NPC level/health and native combat, objective callbacks,
corpse/objective state, atomic rewards and sealed token claims, expiration/abandonment handling,
logout/login reconstruction and persisted-state reload.

Authored quests: editable loading/lookup/validation; offer and acceptance; prerequisite/objective state;
direct-item actions; transactional inventory/stat rewards; timed turn-in, account cooldown and flags;
journal restoration and DAO reload. Invalid/unknown commits do not publish success or replay rewards.

Primary existing coverage: AuthoredQuestTests; GeneratedMissionServiceTests;
GeneratedMissionRollProjectionTests; GeneratedMissionMaterializationTests; GeneratedMissionLoginPlanTests;
GeneratedMissionTokenPolicyTests; mission corpse tests; MissionContentEditabilityTests;
InteractionEditabilityTests; real disposable MissionDaoValidation.

## Level content provenance

The compiled starting source identifies `AORebirth/GameData/Missions/Source/MissionLevels.csv` as its source.
The actual file SHA256 is `295ade2cac00ddfc975bbf1c3f0d7f953f3726e08cc21c0c1f32a5b5b30eb70f`,
matching that declaration. No mission-level cells were transcribed or invented. Existing validation
limits are represented separately as editable policy with explicit starting-source provenance.

## Direct native consumer inventory

23 distinct files, relative to `AORebirth/Server/ZoneEngine_New/Core/`:

- Dialogue/ContentRegistry.cs
- Dialogue/ContentValidation.cs
- MessageHandlers/QuestAlternativeMessageHandler.cs
- MessageHandlers/ZoneLoginHandler.cs
- Missions/AuthoredQuestCatalog.cs
- Missions/AuthoredQuestService.cs
- Missions/AuthoredQuestService.TimedTurnIn.cs
- Missions/Content/MissionAcgCapturedLayoutCatalog.cs
- Missions/Content/MissionAcgCorpseCreditPolicy.cs
- Missions/Content/MissionAcgLayoutCatalog.cs
- Missions/Content/MissionAcgObjectiveContracts.cs
- Missions/Content/MissionAcgRuntimeMaterialization.cs
- Missions/Content/MissionAcgTokenRewardPolicy.cs
- Missions/Content/MissionOfferCompatibility.cs
- Missions/Content/MissionOfferTextBuilder.cs
- Missions/Content/MissionRewardEvidenceModel.cs
- Missions/Content/MissionRollEvidenceCatalog.cs
- Missions/GeneratedMissionAcgService.cs
- Missions/GeneratedMissionCorpseProjection.cs
- Missions/GeneratedMissionRollProjection.cs
- Missions/GeneratedMissionWorld.cs
- Missions/InteractionContent.cs
- Playfield/PlayfieldManager.cs

Historical source-linked test consumers are tracked separately from these 23 runtime consumers.

## Completed changes

All 35 retained files were deleted. Two dependent obsolete helpers were also removed:
`Core/Missions/AuthoredMissionTransactionScope.cs` and
`Core/Missions/Content/MissionAcgObjectiveContracts.cs`.
The first was another adapter around the transaction already owned by the native service;
the second retained an inactive objective-completion implementation.

Required responsibilities now live in native mission infrastructure. Mission persistence calls the
accepted DAO transaction directly. Generated mission bindings use the accepted DAO DTO rather than
duplicate immutable/state wrapper objects. Compatible quest/layout/content schema fields remain;
no old runtime algorithm was copied, renamed or wrapped as its replacement.

Existing policy values are editable data in `Generation.json`, `RollPolicy.json`, and
`Source/MissionLevelRules.json`, each with starting-source provenance. Provenance is documentation,
not runtime authorization. No source hashes gate edited content. The existing mission-level CSV
is copied into output and read directly; no generated C# mission table remains.

The configured offer and accepted lifetimes preserve the starting 48-hour behavior. Acceptance
starts a fresh configured lifetime independently of the offer's remaining window. Native NPC combat,
inventory transactions, mission reward transactions, login ownership and other subsystems were not redesigned.

## Validation and test accounting

- NewEngine build: PASS.
- Focused missions: 94 passed; 1 identical pre-existing materialization failure.
- Focused quests/editability: 28 passed.
- Full NewEngine: 578 passed / 20 failed. The 20 failure names and error messages match the exact
  starting SHA; no new or changed failure signature and no missing starting failure.
- Real disposable DAO checks: 275 passed, including rollback/concurrency/reload.
- No existing NewEngine test method was removed.
- One direct regression was added to the existing `MissionContentEditabilityTests` file: loading
  edited level values from CSV with the same binary, and rejecting invalid cells/difficulty.
- Forty existing level/roll/packet tests were adapted and source-linked into the existing
  NewEngine test project. No new test files, framework, fixture system or synthetic DAO were added.
- The old fee arithmetic test now checks the actual configured fee. Durable deduction and
  insufficient-funds assertions are covered by the existing real DAO validation.

Only these five obsolete compiled-authority/publication tests were retired (`OBSOLETE_LEGACY_TEST`):

1. `PayloadAndMetadataHashValidationFailsClosed`
2. `CanonicalSerializationAndGenerationAreDeterministic`
3. `CanonicalSourceHashAndDeployedCsvNormalizationRemainStable`
4. `FailedPublicationNeverExposesPartialGraph`
5. `ConcurrentReadersRetainValidSnapshotDuringFailedReload`

All 45 methods from the three adapted historical files are accounted for: 40 retained,
5 obsolete. Missing/duplicate/out-of-range/invalid/descending level data, exact accepted values,
NPC scaling, mission types, signed sliders, durable identity allocation, fees, rewards,
deterministic generation, captured packet envelopes and binary round trips retain coverage.
A missing final newline is no longer treated as corrupted game data; the truncation test rejects
an actually missing last field instead of asserting that obsolete canonical-hash formatting rule.

The documented DAO wrapper initially failed because its project selected C# 7.3 while an existing
shared character contract uses nullable-reference annotations. Running the same disposable harness
with the command-line `-p:LangVersion=latest` override passed. The harness/project/provider/schema
sources were not changed. No existing database was migrated; only the test-owned disposable database
used by the established validation was created and removed.

## Explicit limitation: separate historical test project

The .NET Framework AOtomation test project directly links the retired runtime files. It is not
buildable against this removal without a separate migration of that historical fixture network.
This task's required build target is NewEngine, and its required persistence validation is the
existing actual-DAO harness. No broader framework conversion or blanket test retirement was performed.

The initial dependency inventory found 269 methods across 19 historical classes. The three files
adapted here account for 45 of them as described above. The other 224 methods across 16 classes
remain in source; they were not silently deleted or declared obsolete. Their historical project
remains blocked. Relevant existing native tests cover current runtime behavior, but this report
does not claim one-to-one replacement of that whole historical suite.

Remaining historical classes: `MissionDaoArchitectureTests`, `MissionAcgLayoutCatalogTests`,
`MissionAcgBindingPersistenceTests`, `MissionAcgAcceptedProjectionTests`, `MissionOfferStoreTests`,
`MissionAcgRuntimeMaterializationTests`, `MissionAcgObjectiveCompletionTests`,
`MissionAcgOperationalRuntimeTests`, `MissionAcgSpatialRuntimeTests`, `MissionAcgExpiryStateStoreTests`,
`MissionAcgExpiryRuntimeContractTests`, `MissionAcgTokenProgressStoreTests`,
`MissionAcgTokenProgressRuntimeContractTests`, `PersistentMissionFoundationTests`,
`QuestRuntimePersistenceTests`, `AreteFrameworkBootstrapTests`.

No production operations, client launch, push, database schema source changes, or unrelated
baseline-failure repairs were performed. Gameplay preservation is supported by source reconciliation
and automated tests; this is not official-client or production acceptance.
