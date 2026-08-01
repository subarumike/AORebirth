namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// Acceptance-level guards for the complete capture-backed PF127 Subway
    /// surface. These checks bind accepted evidence to compiled production
    /// owners and to the focused behavioral suites that exercise them.
    /// </summary>
    [TestClass]
    public class SubwayAcceptanceMatrixTests
    {
        [TestMethod]
        public void AllSupportedOrdinaryActorsReconcileThroughProductionOwners()
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider());
            OrdinaryEnemySpawnDefinition[] spawns = catalog.GetSpawns();

            Assert.AreEqual(322, spawns.Length);
            Assert.AreEqual(
                322,
                spawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Active));
            Assert.AreEqual(
                0,
                spawns.Count(value => value.Disposition == OrdinaryEnemyRuntimeDisposition.Quarantined));
            Assert.AreEqual(322, spawns.Select(value => value.SpawnKey).Distinct().Count());
            Assert.IsTrue(spawns.All(value => value.LevelDefinition.IsValid));

            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\CapturedEnemyCombatProfileCatalogTests.cs",
                "complete PF127 combat resolution",
                "FinalOrdinaryDungeonCombatCompletionReconcilesAllTwentyFiveActorsAndAll489Resolve",
                "ActiveSubwayAndTempleSpawnsAreEitherExactSourceCertifiedOrFailClosed",
                "DiscardedPetUsesReusableNaturalArchetypeAcrossAllActiveLevels",
                "WorkmanStrikerEveryActiveAtomicGenerationResolvesItsCapturedWeaponArchetype");
        }

        [TestMethod]
        public void ProductionOwnersAreCompiledAndReachableFromThePlayfieldLifecycle()
        {
            AssertFileContainsAll(
                @"AORebirth\Server\ZoneEngine\ZoneEngine.csproj",
                "compiled Subway production ownership",
                @"Core\Playfields\Content\SubwayContentModule.cs",
                @"Core\Playfields\CapturedSubwayContentProvider.cs",
                @"Core\Playfields\CapturedSubwayOrdinaryContentProvider.cs",
                @"Core\Playfields\CapturedSubwayEncounterRuntimeService.cs",
                @"Core\Playfields\CapturedSubwayVendorContentProvider.cs",
                @"Core\Playfields\CapturedSubwayVendorRuntimeService.cs",
                @"Core\Playfields\CapturedSubwayTailorDialogueRuntime.cs",
                @"Core\Playfields\CapturedPlayfieldDoorStatusRuntimeService.cs",
                @"Core\Playfields\Pf127CollisionGeometryLoader.cs",
                @"Core\Navigation\Pf127ChaseNavigationProvider.cs",
                @"Core\Functions\GameFunctions\SubwayTeleportProxyDestinationRules.cs",
                @"Core\Subway\Quests\WindcallerKarrecQuestRuntime.cs",
                @"Core\Subway\Quests\WindcallerKarrecTradeAdapter.cs",
                @"Core\Subway\Quests\WindcallerKarrecPacketSender.cs",
                @"Core\Subway\Quests\TotwGatewayInteractionHandler.cs");

            AssertFileContainsAll(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs",
                "Subway activation and teardown",
                "new SubwayContentModule()",
                "this.windcallerKarrecNpcs.Spawn(",
                "this.vendors.SpawnCapturedSubwayVendors(",
                "this.windcallerKarrecNpcs.Clear(",
                "this.vendors.ClearCapturedSubwayVendors(",
                "this.npcRuntime.ClearRuntimeState()");

            AssertFileContainsAll(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs",
                "Subway population and encounter lifecycle",
                "new CapturedSubwayContentProvider()",
                "new CapturedSubwayOrdinaryContentProvider()",
                "new CapturedSubwayEncounterRuntimeService(",
                "this.worldPopulation.ActivatePlayfield(",
                "this.capturedSubwayEncounters.ProcessDue(",
                "this.worldPopulation.ClearPlayfield(");

            AssertFileContainsAll(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayRetaliationEligibilityResolver.cs",
                "exact PF127 retaliation eligibility owner",
                "class CapturedSubwayRetaliationEligibilityResolver",
                "TryResolveExact(",
                "DiscardedPetRetaliationEvidence",
                "MuggerRetaliationEvidence");

            AssertFileContainsAll(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs",
                "production PF127 combat resolver call path",
                "ResolveCombatContractForSpawn(",
                "CapturedSubwayRetaliationEligibilityResolver.TryResolveExact(",
                "retaliationEligibilityPromoted");
        }

        [TestMethod]
        public void PopulationMovementAggroCombatAndLifecycleHaveFocusedCoverage()
        {
            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\WorldPopulationFoundationTests.cs",
                "ordinary population and lifecycle tests",
                "CapturedOrdinaryExceptionsAndPopulationBoundaryRemainStable",
                "PopulationControllerPreventsDuplicateAndStaleGenerationRespawnRequests",
                "NewGenerationClearsPriorDeathCorpseFailureAndScheduleState",
                "VisibilityCombatCorpseAndNavigationOwnersCannotSelectOrAdvanceLevels");

            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\NpcChaseNavigationTests.cs",
                "PF127 chase, LOS, leash, interruption, and fallback tests",
                "SubwayLeashResetsWhenNpcOrTargetLeavesHomeBoundary",
                "StuckRouteInvalidatesWithinBoundedTime",
                "Pf127CapturedWallBlocksAttackLine",
                "Pf127PlansCollisionValidRouteAroundRepresentativeVergilWall",
                "Pf127ReturnToHomeUsesSharedCollisionValidRouting",
                "PlayfieldResetAndRuntimeDisposalClearAllRoutes");

            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\PlayfieldLifecycleTraceTests.cs",
                "captured patrol and lifecycle wiring tests",
                "NpcPatrolReplayCoordinator",
                "CapturedSubwayContentProvider",
                "ClearNpcRuntimeState");
        }

        [TestMethod]
        public void NamedDeathRespawnCorpseAndLootOwnersHaveFocusedCoverage()
        {
            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\AbmouthEncounterRuntimeServiceTests.cs",
                "PF127 named encounter tests",
                "DedicatedEncounterOwnsAbmouthAndOrdinaryPopulationRejectsBossesAndSummons",
                "LeashResetCancelsBossEncounterStateAndLivingSummons",
                "NamedBossesRespawnTenMinutesAfterDeathIndependentlyOfCorpses",
                "StrikeForemanUsesCapturedSpawnExactCombatAndSharedNamedLifecycle",
                "VergilHealingUsesCapturedNanoValuesAndPausesWeaponCombatTicks");

            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\DungeonNamedLifecycleCompletionTests.cs",
                "named lifecycle tests",
                "EveryNamedDeathCreatesAtMostOneCorpse",
                "EveryNamedDeathPerformsAtMostOneAtomicLootRoll",
                "CorpseReopenDoesNotRerollLoot",
                "Pf127AndPf1931SchedulesRemainIndependent",
                "StrikeForemanLifecycleContractRemainsExact");

            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\SubwayEnemyLootEvidenceTests.cs",
                "ordinary corpse and loot tests",
                "DisobedientBotUsesOnlyStrictlyProvenObservedItems",
                "BloodcreeperUsesFourReviewedOpensAndKeepsItsPoolIncomplete",
                "OfficialDeathLinkedCorpseEvidenceReachesEveryAuditedOrdinaryProfile",
                "AmbiguousOrUnresolvedLinkageCannotBecomeActiveLoot");

            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\GlobalLootFoundationTests.cs",
                "global atomic loot tests",
                "VergilObservedCorpseSnapshotsGenerateOnlyExactLinkedBundles",
                "StrikeForemanObservedSnapshotsUseEnemyLevelWithinItemQlBounds",
                "ObservedCorpseSnapshotsRejectIndependentProbabilityDefinitions");
        }

        [TestMethod]
        public void VendorDialogueQuestAndGatewayOwnersAreCompiledAndFocused()
        {
            AssertFileContainsAll(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\KnuBotTradeMessageHandler.cs",
                "Karrec trade dispatch",
                "WindcallerKarrecTradeAdapter.TryStageTradeItem(");
            AssertFileContainsAll(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\KnuBotFinishTradeMessageHandler.cs",
                "Karrec finish-trade dispatch",
                "WindcallerKarrecTradeAdapter.TryFinishTrade(");
            AssertFileContainsAll(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldInteractionRuntimeService.cs",
                "Subway interaction dispatch",
                "CapturedSubwayVendorInteractionHandler.Default.TryHandleUse(",
                "TotwGatewayInteractionHandler.Default.TryHandleUse(");

            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\SubwayVendorContentTests.cs",
                "vendor and Tailor tests",
                "CaptureDefinesSixNpcOwnersAndSixResolvedShopEndpoints",
                "CapturedShopStocksPreserveAll202RowsAndContiguousSlots",
                "CapturedShopStockFingerprintMatchesAuthoritativeCsv",
                "AlternateCapturedShopSnapshotIsAtomicAndMatchesAuthoritativeCsv",
                "AlternateCapturedSnapshotDoesNotReplaceCanonicalRuntimeStock",
                "CapturedSnapshotResolutionFailsClosedOutsideExactEvidence",
                "TailorMeasurementChoicesMapToEightCapturedQlOneItems",
                "TailorFirstOpenAndReopenResolveToCapturedGreetingNodes");

            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\WindcallerKarrecNpcContentTests.cs",
                "Karrec NPC and patrol tests",
                "CaptureDefinesExactlyTheThreeQuestNpcsInPlayfield655",
                "EverySpawnDefinitionResolvesToCheckedInDialogueContent",
                "RuntimeRegistryPreventsDuplicateEntriesAndTearsDownByPlayfield");
            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\WindcallerKarrecInteractionRulesTests.cs",
                "Karrec interaction tests",
                "ExactPlayerPlayfieldNpcStateAndTwoOfferingsAreRequired",
                "WrongNpcAndWrongItemCombinationsFailClosed",
                "GatewayRequiresExactTerminalIdentityTypeAndInstance");
            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\QuestRuntimePersistenceTests.cs",
                "Karrec persistence tests",
                "KarrecProgressRewardsAndAccountAccessAreScopedAndRetrySafe",
                "KarrecTokenRetryUsesTheAppliedTierInsteadOfTheNewLiveTier",
                "NeutralKarrecTokenDecisionRemainsZeroAfterSidedRetry");
        }

        [TestMethod]
        public void PlayfieldGeometryZoningAndTeardownHaveFocusedCoverage()
        {
            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\PlayfieldCollisionGeometryTests.cs",
                "PF127 geometry and attack-line tests",
                "ReviewedPf127AssetLoadsAndReplaysCapturedVergilClearAndBlockedSegments",
                "ActivatedSafetyPolicyCoversVergilAndExplicitContractOptInsOnly",
                "CombatWiringGatesNormalAndParallelDamageWithoutClearingAggro");
            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\PlayfieldLifecycleTraceTests.cs",
                "PF127 zoning and teardown tests",
                "SubwayProxyExitUsesOfficialLandingAndSuppressesDelayedEntryBounce",
                "CapturedSubwayEntryRadius = 4.0f");
            AssertFileContainsAll(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\TempleDoorStatusRuntimeTests.cs",
                "PF127 captured door snapshot tests",
                "SubwayDoorEvidencePreservesExactCapturedIdentityAndStateCoverage",
                "SubwayExternalArrivalEvidenceMapsExactlySixOfficialStatels",
                "SubwayExternalArrivalSendsOnlySixCapturedClosedStatuses",
                "SubwayDoorRuntimeDoesNotReplayOnDeathOrInventProximity");

            AssertFileContainsAll(
                @"docs\evidence\SUBWAY_FULL_CORPUS_COMPLETION_20260731.md",
                "authoritative Subway evidence boundary",
                "322/322",
                "Explicit non-blocking evidence gaps",
                "PF127 dynamic door transitions",
                "No available evidence was ignored");
        }

        private static void AssertFileContainsAll(
            string relativePath,
            string owner,
            params string[] values)
        {
            string source = Read(relativePath);
            foreach (string value in values)
            {
                StringAssert.Contains(source, value, owner + " is missing " + value + ".");
            }
        }

        private static string Read(string relativePath)
        {
            string path = Path.Combine(FindRepositoryRoot(), relativePath);
            Assert.IsTrue(File.Exists(path), "Missing acceptance owner or focused test: " + relativePath);
            return File.ReadAllText(path);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
