@echo off
setlocal

rem Retained Temple packet/evidence/shared-mechanic acceptance.
rem Current NewEngine gameplay is covered by the mandatory NewEngine test gate.
set "RUNNER=%~dp0run_aotomation_messaging_tests.cmd"

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~TempleAcceptanceMatrixTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatActiveCoverageTests.Pf1931CoverageIncludesEveryOrdinaryNamedSuccessorAndOwnedAdd"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~TempleOfThreeWindsOrdinaryContentTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~DungeonNamedEncounterCompletionTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~DungeonNamedLifecycleCompletionTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~TempleDoorStatusRuntimeTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatProfileCatalogTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~OrdinaryEnemyCombatSetupGeneratorTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~PlayfieldCollisionGeometryTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~NpcChaseNavigationTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~OfficialDungeonNavigationTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~N3RecoveredContractTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~PlayfieldRuntimeOwnershipTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.AzturRoomBossesUseTheSharedFactoryWithCaptureExactBytes"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.ReanimatedCorpseAnchorProfilesUseTheCapturedSharedPacketSequence"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.CultistResolutionRejectsMissingNearestAndCrossEnemyEvidence"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.Level48DeathlessUsesCalculatedDamageWithExactArchetypePacketSemantics"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.LevelThirtyTwoCultistUsesProductionWeaponValuesWithExactCapturedPacketSequence"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatPacketFactoryTests.TempleOrdinaryCoverageRestoresEveryCompleteContract"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.RegistryRejectsDuplicateMissingInvalidAndEvidenceUnsafeDefinitions"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.GuaranteedIndependentWeightedQualityQuantityAndUniqueGenerationAreDeterministic"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ObservedCorpseSnapshotsRejectIndependentProbabilityDefinitions"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.NoAssignmentUnresolvedAndOwnedSummonPathsFailClosed"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.CreditsNoneFixedRangeAndUnresolvedRemainDistinct"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ObservedCreditSetsRemainUniqueWhileObservedSamplesPreserveMultiplicity"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ArchitectureGuardrailsKeepLootOwnershipOutOfPlayfieldAndEnemyBranches"
if errorlevel 1 exit /b %errorlevel%

exit /b 0
