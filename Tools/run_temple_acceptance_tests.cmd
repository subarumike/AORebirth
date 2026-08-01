@echo off
setlocal

set "RUNNER=%~dp0run_aotomation_messaging_tests.cmd"

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~TempleAcceptanceMatrixTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatActiveCoverageTests.Pf1931CoverageIncludesEveryOrdinaryNamedSuccessorAndOwnedAdd"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~TempleOfThreeWindsOrdinaryContentTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~DungeonNamedEncounterCompletionTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~DungeonNamedLifecycleCompletionTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~TempleDoorStatusRuntimeTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatProfileCatalogTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~OrdinaryEnemyCombatSetupGeneratorTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldCollisionGeometryTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~NpcChaseNavigationTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~OfficialDungeonNavigationTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~N3RecoveredContractTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldRuntimeOwnershipTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.TempleContentModuleActivatesCapturedNpcSpawnsOnlyForPf1931"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.AzturRoomBossesUseTheSharedFactoryWithCaptureExactBytes"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.ReanimatedCorpseAnchorProfilesUseTheCapturedSharedPacketSequence"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.CultistResolutionRejectsMissingNearestAndCrossEnemyEvidence"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.Level48DeathlessUsesCalculatedDamageWithExactArchetypePacketSemantics"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.LevelThirtyTwoCultistUsesProductionWeaponValuesWithExactCapturedPacketSequence"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.TempleOrdinaryCoverageRestoresEveryCompleteContract"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.RegistryRejectsDuplicateMissingInvalidAndEvidenceUnsafeDefinitions"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.GuaranteedIndependentWeightedQualityQuantityAndUniqueGenerationAreDeterministic"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ObservedCorpseSnapshotsRejectIndependentProbabilityDefinitions"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.NoAssignmentUnresolvedAndOwnedSummonPathsFailClosed"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.CreditsNoneFixedRangeAndUnresolvedRemainDistinct"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ObservedCreditSetsRemainUniqueWhileObservedSamplesPreserveMultiplicity"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ArchitectureGuardrailsKeepLootOwnershipOutOfPlayfieldAndEnemyBranches"
if errorlevel 1 exit /b %errorlevel%

exit /b 0
