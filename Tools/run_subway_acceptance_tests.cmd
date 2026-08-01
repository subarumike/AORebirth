@echo off
setlocal

set "RUNNER=%~dp0run_aotomation_messaging_tests.cmd"

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~SubwayAcceptanceMatrixTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatProfileCatalogTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedEnemyCombatActiveCoverageTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~CapturedSubwayRetaliationEligibilityResolverTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~WorldPopulationFoundationTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~NpcChaseNavigationTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldCollisionGeometryTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.SubwayContentModuleRegistersCapturedNpcSpawnsWithoutOwningRuntimeSystems"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.SubwayExistingPopulationAndPatrolReplayRemainLoaded"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.SubwayOrdinaryLifecyclePolicyIsUniformAndBossesRemainSeparate"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.PvpAuthorizationDoesNotBlockHostileNpcRetaliationInHighGas"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.SubwayProxyExitUsesOfficialLandingAndSuppressesDelayedEntryBounce"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.RedundantScanSupportNanoRuntimeKeepsCapturedPacketOrderAndReversibleOwnedState"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.FragmentedSoulNano95447UsesDynamicSkillAndOwnedOrdinaryAllyLifecycle"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~PlayfieldLifecycleTraceTests.IncompleteRebuildNano90405KeepsCapturedPeriodicHitAndCombatPolicy"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~TempleDoorStatusRuntimeTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~AbmouthEncounterRuntimeServiceTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~DungeonNamedEncounterCompletionTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~DungeonNamedLifecycleCompletionTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~SubwayEnemyLootEvidenceTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~SubwayLootPoolRulesTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~SubwayVendorContentTests"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~WindcallerKarrec"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~QuestRuntimePersistenceTests.Karrec"
if errorlevel 1 exit /b %errorlevel%

call "%RUNNER%" /TestCaseFilter:"FullyQualifiedName~GlobalLootFoundationTests"
if errorlevel 1 exit /b %errorlevel%

exit /b 0
