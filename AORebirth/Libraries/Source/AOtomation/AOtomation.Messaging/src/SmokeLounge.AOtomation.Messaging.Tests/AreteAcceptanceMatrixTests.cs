namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    #endregion

    /// <summary>
    /// Acceptance-level ownership guards for the complete capture-backed Arete surface.
    /// These tests intentionally verify the production dispatch graph as well as the
    /// focused behavior suites so tracked-but-uncompiled content cannot be reported as owned.
    /// </summary>
    [TestClass]
    public class AreteAcceptanceMatrixTests
    {
        [TestMethod]
        public void CapturedPopulationOwnersAreCompiledAndActivatedForBothAretePlayfields()
        {
            string project = Read(@"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");
            AssertContainsAll(
                project,
                "ZoneEngine production compile ownership",
                @"Core\Playfields\AreteLandingSpawn.cs",
                @"Core\Playfields\AreteLandingPopulationEnsure.cs",
                @"Core\Playfields\AreteAlienAreaMobRuntime.cs",
                @"Core\Playfields\AreteSandstormMarauderRuntime.cs",
                @"Core\Playfields\Content\AreteContentModule.cs",
                @"Core\Playfields\Content\CrashedAlienShipContentModule.cs",
                @"Core\MessageHandlers\CrashedAlienShipDoorInteractionHandler.cs");

            string runtime = Read(@"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs");
            AssertContainsAll(
                runtime,
                "Arete population lifecycle owner",
                "AreteAlienAreaMobRuntime.StartForPlayfield(",
                "AreteSandstormMarauderRuntime.StartForPlayfield(",
                "AreteAlienAreaMobRuntime.TickRespawn(",
                "AreteAlienAreaMobRuntime.ClearPlayfield(",
                "AreteSandstormMarauderRuntime.ClearPlayfield(");

            string runtimeSystems = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldRuntimeSystems.cs");
            StringAssert.Contains(runtimeSystems, "new AreteContentModule()");
            StringAssert.Contains(runtimeSystems, "new CrashedAlienShipContentModule()");

            string interaction = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldInteractionRuntimeService.cs");
            StringAssert.Contains(
                interaction,
                "CrashedAlienShipDoorInteractionHandler.Default.TryHandleUse(");

            string door = Read(
                @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CrashedAlienShipDoorInteractionHandler.cs");
            AssertContainsAll(
                door,
                "PF8009 captured door owner",
                "CrashedAlienShipPlayfieldId = 8009",
                "AreteLandingPlayfieldId = 6553",
                "ExitDoorInstance = unchecked((int)0xC0001F49)",
                "EntryDoorInstance = unchecked((int)0x108CD4D0)",
                "SendCapturedCrashedAlienShipDoorExit(",
                "SendCapturedCrashedAlienShipDoorEntry(");
        }

        [TestMethod]
        public void AlienAndSandstormOwnersPreserveExactPopulationAndFailClosedOnUnknownRespawn()
        {
            string alien = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteAlienAreaMobRuntime.cs");
            Assert.AreEqual(
                64,
                CountOccurrences(alien, "new MobSlot("),
                "The complete captured alien-area population must remain exact.");
            AssertContainsAll(
                alien,
                "CapturedWildlifeRespawnSeconds = 40.0",
                "TryResolveRespawnSeconds(",
                "slot.Kind == MobKind.Rollerrat",
                "\"Angry Minibull\"",
                "CapturedAreteAggroCatalog",
                "arete-alien-area-complete-corpus-catalog",
                "CapturedEnemyCombatContract.Unresolved(",
                "SetStat(mob, StatIds.xp, 0);",
                "SetStat(mob, StatIds.mindamage, 0);",
                "SetStat(mob, StatIds.maxdamage, 0);");
            Assert.IsFalse(alien.Contains("DefaultRespawnSeconds = 60.0"));
            Assert.IsFalse(alien.Contains("WildlifeAggroRadiusMeters = 5.0f"));
            Assert.IsFalse(alien.Contains("ResolveCaptureDamage("));
            Assert.IsFalse(alien.Contains("ResolveCaptureXp("));
            Assert.IsFalse(alien.Contains("provisional spider-tier"));
            Assert.IsFalse(alien.Contains("minDamage = 9;"));
            Assert.IsFalse(alien.Contains("maxDamage = 14;"));
            Assert.IsFalse(alien.Contains("minDamage = 6;"));
            Assert.IsFalse(alien.Contains("maxDamage = 10;"));

            string alienXp = Read(
                @"AORebirth\Server\ZoneEngine\Core\AlienXpRuntimeService.cs");
            AssertContainsAll(
                alienXp,
                "Arete Alien Spider AIXP evidence",
                "Capture 20260726-230559: four Alien Spider - Zix kills each add 150 AIXP.",
                "AreteAlienSpiderAixpReward = 150",
                "target.Playfield.Identity.Instance == 6553",
                "return 0;");
            Assert.IsFalse(alienXp.Contains("AlienSpiderTestAixpReward = 5000"));

            string sandstorm = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteSandstormMarauderRuntime.cs");
            AssertContainsAll(
                sandstorm,
                "Capture 20260801-SANDSTORM SANDSTORM Marauders + Control Tower",
                "MarauderName = \"SANDSTORM Marauder\"",
                "MarauderLevel = 7",
                "\"arete-sandstorm-20260801-SANDSTORM\"",
                "CapturedEnemyCombatRuntime.Prepare(");
            Assert.AreEqual(5, CountOccurrences(sandstorm, "new MarauderSlot("));
            Assert.AreEqual(0, CountOccurrences(sandstorm, "new ReplacementDefinition("));
            AssertContainsAll(
                sandstorm,
                "Capture 20260801-SANDSTORM first-seen path actors",
                "new MarauderSlot(265822, DefaultMarauderHeadMesh, 4033.377f, 0.010f, 667.7479f)",
                "new MarauderSlot(265822, DefaultMarauderHeadMesh, 4033.406f, 0.010f, 676.7122f)",
                "new MarauderSlot(287217, 0, 4039.895f, 0.6299585f, 696.3529f)",
                "new MarauderSlot(287217, 0, 4058.394f, 0.610f, 678.1385f)",
                "new MarauderSlot(265822, DefaultMarauderHeadMesh, 4055.279f, 2.131286f, 650.3979f)",
                "MarauderRespawnSeconds = 30.0");
            Assert.IsFalse(sandstorm.Contains("RespawnSeconds = 45.0"));
        }

        [TestMethod]
        public void MovementAggroCombatDeathRespawnAndLootHaveOwnersAndFocusedCoverage()
        {
            string runtime = Read(@"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs");
            AssertContainsAll(
                runtime,
                "movement, aggro, and death runtime ownership",
                "new CapturedAreteMovementRuntimeService()",
                "CapturedAreteAggroCatalog.LoadDefault()",
                "this.capturedAreteMovement.Activate(character)",
                "this.capturedAreteMovement.TryProcessSpawn(",
                "this.capturedAreteMovement.TryProcessPatrol(",
                "this.capturedAreteMovement.TryProcessCombat(",
                "this.capturedAreteMovement.TryProcessLeash(",
                "this.capturedAreteAggro.TryGetRadius(",
                "this.capturedAreteAggro.TryGetEligibility(",
                "internal void BeginNpcDeath(",
                "this.corpseLifecycle.ScheduleDeadNpcDespawn(");

            string movementTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\CapturedAreteMovementRuntimeTests.cs");
            AssertContainsAll(
                movementTests,
                "focused movement and aggro coverage",
                "CommittedCatalogLoadsAllPromotableObservationsAndExcludesScripted",
                "PatrolVariantSelectionIsDeterministicPerSpawnGeneration",
                "LifecycleConditionsActivateOnlyTheirCapturedBehavior",
                "IdleControllerCanEnterCapturedPatrolWithoutWaypointState",
                "CapturedAggroCatalogLoadsNpcFirstDistancesAndFailsClosed",
                "TerminalSchema4PatrolFallsBackWithoutEnteringLegacyReplayWrap",
                "InterruptedSequenceFallsBackUntilLifecycleBehaviorChanges",
                "MetadataMismatchFailsClosedAndRemovesRegeneratedRuntimeIdentity");

            string landing = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\AreteLandingSpawn.cs");
            AssertContainsAll(
                landing,
                "captured combat owner",
                "CapturedEnemyCombatProfileCatalog",
                "TryCreateExactCapturedAttackOnSightContract(",
                "CapturedEnemyCombatRuntime.Prepare(");

            string combatCatalogTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\CapturedEnemyCombatProfileCatalogTests.cs");
            string combatCoverageTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\CapturedEnemyCombatActiveCoverageTests.cs");
            StringAssert.Contains(combatCatalogTests, "Angry Minibull");
            StringAssert.Contains(combatCatalogTests, "Engineer Automaton I");
            StringAssert.Contains(combatCoverageTests, "Alien Spider - Zix");
            StringAssert.Contains(combatCoverageTests, "SANDSTORM Marauder");

            string lifecycleTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\AreteFrameworkBootstrapTests.cs");
            AssertContainsAll(
                lifecycleTests,
                "focused Arete respawn coverage",
                "CapturedAreteRespawnIntervalsRemainScopedToProvenNpcKinds",
                "CapturedNamedEnemyLifecycleUsesMeasuredReplacementDelays");

            string loot = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\GlobalLootRuntimeService.cs");
            AssertContainsAll(
                loot,
                "captured Arete loot owner",
                "AretePlayfieldId = 6553",
                "BuildArete104809DockerSnapshots()",
                "BuildArete104809WasteCollectorSnapshots()",
                "BuildArete104809GarbageFleaSnapshots()",
                "BuildArete104809CleaningRobotSnapshots()",
                "BuildArete152454ResolvedGnarlSnapshot()");

            string lootTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\GlobalLootFoundationTests.cs");
            AssertContainsAll(
                lootTests,
                "focused Arete loot coverage",
                "AretePartOneVariantLootRemainsNameScopedAndAtomic",
                "Arete104809OrdinaryLootPreservesEveryIdentityLinkedAtomicSnapshot",
                "Arete152454BlankNameCorpseRowsRemainCorrelatedAndAtomic",
                "AretePartTwoLootRemainsPlayfieldScopedIdentityLinkedAndAtomic");
        }

        [TestMethod]
        public void VendorInteractionDialogueQuestMissionAndPlayfieldOwnersHaveFocusedCoverage()
        {
            string vendors = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVendorRuntimeService.cs");
            AssertContainsAll(
                vendors,
                "captured Arete vendor owner",
                "CapturedAreteMarcoSpidaVendorRuntimeService",
                "CapturedAreteLoreleiVendorRuntimeService",
                "CapturedAreteAntonioStacklundVendorRuntimeService",
                "CapturedAreteRemiGalloisVendorRuntimeService",
                "CapturedAreteSarahGreeneVendorRuntimeService");

            string vendorTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\AreteCapturedVendorOwnershipTests.cs");
            AssertContainsAll(
                vendorTests,
                "focused Arete vendor coverage",
                "Antonio",
                "Remi",
                "Sarah",
                "CapturedVendorUseDispatchIsExactOrderedAndFailsClosed");

            string interactions = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldInteractionRuntimeService.cs");
            AssertContainsAll(
                interactions,
                "captured Arete interaction dispatch",
                "CapturedAreteMarcoSpidaVendorInteractionHandler.Default.TryHandleUse(",
                "CapturedAreteLoreleiVendorInteractionHandler.Default.TryHandleUse(",
                "CapturedAreteAntonioStacklundVendorInteractionHandler.Default.TryHandleUse(",
                "CapturedAreteRemiGalloisVendorInteractionHandler.Default.TryHandleUse(",
                "CapturedAreteSarahGreeneVendorInteractionHandler.Default.TryHandleUse(",
                "CrashedAlienShipDoorInteractionHandler.Default.TryHandleUse(");

            string exactInteractionTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\CapturedAreteExactInteractionTests.cs");
            AssertContainsAll(
                exactInteractionTests,
                "focused exact-interaction coverage",
                "ExactReplyCatalogPreservesFiniteCapturedSequencesAndFailsClosed",
                "CapturedJunePackLoadsExactOptionsWithoutInventingPromptOrAnswerSemantics");

            string dialogue = Read(
                @"AORebirth\Server\ZoneEngine\Core\Arete\Dialogue\ContentDrivenNpcDialogueRouter.cs");
            AssertContainsAll(
                dialogue,
                "captured dialogue and quest dispatch",
                "AntonioStacklundQuestRuntime",
                "KarliCappelleriQuestRuntime",
                "LeonoraMartyQuestRuntime",
                "PatrickSunQuestRuntime",
                "RemiGalloisQuestRuntime",
                "ShinySwordQuestRuntime");

            string questTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\AreteCapturedQuestOwnershipTests.cs");
            AssertContainsAll(
                questTests,
                "focused captured quest coverage",
                "Antonio",
                "Karli",
                "Leonora",
                "Patrick",
                "Remi",
                "Shiny",
                "Fail");

            string frameworkTests = Read(
                @"AORebirth\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging.Tests\AreteFrameworkBootstrapTests.cs");
            AssertContainsAll(
                frameworkTests,
                "focused dialogue, mission, and playfield content coverage",
                "CheckedInBootstrapLoadsAreteAndSubwayDialogueAsOneValidatedSet",
                "RuntimeDefinitionCatalogBuildsCheckedInAreteAndKarrecContracts",
                "DuplicatePacksAcrossManifestsFailClosed",
                "MissingCheckedInContentThrowsWithResolvedValidationDetails");
        }

        private static string Read(string relativePath)
        {
            string path = Path.Combine(FindRepositoryRoot(), relativePath);
            Assert.IsTrue(File.Exists(path), "Missing acceptance owner or focused test: " + relativePath);
            return File.ReadAllText(path);
        }

        private static void AssertContainsAll(string source, string owner, params string[] values)
        {
            foreach (string value in values)
            {
                StringAssert.Contains(source, value, owner + " is missing " + value + ".");
            }
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int cursor = 0;
            while ((cursor = source.IndexOf(value, cursor, StringComparison.Ordinal)) >= 0)
            {
                count++;
                cursor += value.Length;
            }

            return count;
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
