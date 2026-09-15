@echo off
setlocal

rem Retained Subway packet/evidence/shared-mechanic acceptance.
rem Current NewEngine gameplay is covered by the mandatory NewEngine test gate.
set "RUNNER=%~dp0run_aotomation_messaging_tests.cmd"

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~SubwayAcceptanceMatrixTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatProfileCatalogTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatActiveCoverageTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~CapturedSubwayRetaliationEligibilityResolverTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~WorldPopulationFoundationTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~NpcChaseNavigationTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~PlayfieldCollisionGeometryTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~TempleDoorStatusRuntimeTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~AbmouthEncounterRuntimeServiceTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~DungeonNamedEncounterCompletionTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~DungeonNamedLifecycleCompletionTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~SubwayEnemyLootEvidenceTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~SubwayLootPoolRulesTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~SubwayVendorContentTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~WindcallerKarrec"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~QuestRuntimePersistenceTests.Karrec"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.RegistryRejectsDuplicateMissingInvalidAndEvidenceUnsafeDefinitions"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.VergilObservedCorpseSnapshotsGenerateOnlyExactLinkedBundles"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.StrikeForemanObservedSnapshotsUseEnemyLevelWithinItemQlBounds"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ObservedCorpseSnapshotsRejectIndependentProbabilityDefinitions"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.NoAssignmentUnresolvedAndOwnedSummonPathsFailClosed"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ObservedCreditSetsRemainUniqueWhileObservedSamplesPreserveMultiplicity"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /Settings:"%~dp0required-tests.runsettings" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests.ArchitectureGuardrailsKeepLootOwnershipOutOfPlayfieldAndEnemyBranches"
if errorlevel 1 exit /b %errorlevel%

exit /b 0
