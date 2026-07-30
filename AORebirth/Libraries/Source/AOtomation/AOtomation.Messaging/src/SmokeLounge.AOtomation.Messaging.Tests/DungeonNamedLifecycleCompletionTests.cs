namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using AORebirth.Core.Playfields;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class DungeonNamedLifecycleCompletionTests
    {
        [TestMethod]
        public void AllNineteenDomainsHaveOneExplicitRespawnClassification()
        {
            DungeonNamedLifecycleDefinition[] definitions = DungeonNamedLifecycleCatalog.All();
            Assert.AreEqual(19, definitions.Length);
            Assert.AreEqual(19, definitions.Select(value => value.ProfileKey).Distinct().Count());
            Assert.AreEqual(5, definitions.Count(value => value.PlayfieldId == 127));
            Assert.AreEqual(14, definitions.Count(value => value.PlayfieldId == 1931));
            Assert.IsFalse(definitions.Any(
                value => value.Classification
                         == DungeonNamedRespawnClassification.UnresolvedFailClosed));
            foreach (DungeonNamedLifecycleDefinition definition in definitions)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.RespawnOwnerKey));
                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.DelayRule));
                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.LiveRuntimeBehavior));
                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.RuntimeDisposalBehavior));
                Assert.IsFalse(string.IsNullOrWhiteSpace(definition.ReentryBehavior));
            }
        }

        [TestMethod]
        public void FullUkleshKhalumAzturUkleshLifecycleCompletes()
        {
            var lifecycle = new CapturedTempleMainRoomLifecycle();
            Assert.IsTrue(lifecycle.TryMarkSpawned(DungeonNamedLifecycleCatalog.UkleshProfileKey));
            Assert.IsTrue(lifecycle.TryMarkDeath(DungeonNamedLifecycleCatalog.UkleshProfileKey));
            Assert.IsFalse(lifecycle.CanSpawn(DungeonNamedLifecycleCatalog.UkleshProfileKey));
            Assert.IsTrue(lifecycle.TryMarkSpawned(DungeonNamedLifecycleCatalog.KhalumProfileKey));
            Assert.IsTrue(lifecycle.TryMarkDeath(DungeonNamedLifecycleCatalog.KhalumProfileKey));
            Assert.IsFalse(lifecycle.CanSpawn(DungeonNamedLifecycleCatalog.UkleshProfileKey));
            Assert.IsTrue(lifecycle.TryMarkSpawned(DungeonNamedLifecycleCatalog.AzturProfileKey));
            Assert.IsTrue(lifecycle.TryMarkDeath(DungeonNamedLifecycleCatalog.AzturProfileKey));
            Assert.IsTrue(lifecycle.TryScheduleReset());
            Assert.IsTrue(lifecycle.CanSpawn(DungeonNamedLifecycleCatalog.UkleshProfileKey));
            Assert.IsTrue(lifecycle.TryMarkSpawned(DungeonNamedLifecycleCatalog.UkleshProfileKey));
            Assert.AreEqual(CapturedTempleMainRoomPhase.UkleshActive, lifecycle.Phase);
            Assert.IsFalse(lifecycle.CanSpawn(DungeonNamedLifecycleCatalog.UkleshProfileKey));

            string temple = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs");
            Assert.IsTrue(temple.Contains("IsMainRoomStage(state.Definition.ProfileKey)"));
            Assert.IsTrue(temple.Contains(
                "&& !this.mainRoomLifecycle.CanSpawn(state.Definition.ProfileKey)"));
        }

        [TestMethod]
        public void PostAzturResetCreatesExactlyOneUkleshSchedule()
        {
            var scheduler = new DungeonNamedRespawnScheduler();
            DateTime due = new DateTime(2026, 7, 29, 12, 10, 0, DateTimeKind.Utc);
            Assert.IsTrue(scheduler.Schedule(
                1931,
                DungeonNamedLifecycleCatalog.UkleshProfileKey,
                DungeonNamedLifecycleCatalog.AzturProfileKey,
                due));
            Assert.IsFalse(scheduler.Schedule(
                1931,
                DungeonNamedLifecycleCatalog.UkleshProfileKey,
                DungeonNamedLifecycleCatalog.AzturProfileKey,
                due));
            Assert.AreEqual(1, scheduler.TakeDue(due, 19).Length);
            Assert.AreEqual(0, scheduler.TakeDue(due, 19).Length);
        }

        [TestMethod]
        public void KhalumAndAzturHaveNoIndependentRespawn()
        {
            foreach (string key in new[]
            {
                DungeonNamedLifecycleCatalog.KhalumProfileKey,
                DungeonNamedLifecycleCatalog.AzturProfileKey
            })
            {
                DungeonNamedLifecycleDefinition definition = DungeonNamedLifecycleCatalog.Get(key);
                Assert.AreEqual(
                    DungeonNamedRespawnClassification.ExplicitlyNoIndependentRespawn,
                    definition.Classification);
                Assert.AreEqual(
                    DungeonNamedRespawnTrigger.PredecessorDeath,
                    definition.Trigger);
                Assert.IsTrue(definition.OwnerRequired);
            }
        }

        [TestMethod]
        public void RuntimeDisposalCancelsSuccessorAndResetSchedules()
        {
            var scheduler = new DungeonNamedRespawnScheduler();
            DateTime now = DateTime.UtcNow;
            Assert.IsTrue(scheduler.Schedule(
                1931,
                DungeonNamedLifecycleCatalog.KhalumProfileKey,
                DungeonNamedLifecycleCatalog.UkleshProfileKey,
                now));
            Assert.IsTrue(scheduler.Schedule(
                1931,
                DungeonNamedLifecycleCatalog.UkleshProfileKey,
                DungeonNamedLifecycleCatalog.AzturProfileKey,
                now));
            scheduler.CancelPlayfield(1931);
            Assert.AreEqual(0, scheduler.Count);

            var lifecycle = new CapturedTempleMainRoomLifecycle();
            lifecycle.Dispose();
            Assert.AreEqual(CapturedTempleMainRoomPhase.Disposed, lifecycle.Phase);
            Assert.IsFalse(lifecycle.TryMarkSpawned(DungeonNamedLifecycleCatalog.UkleshProfileKey));
        }

        [TestMethod]
        public void OwnedAddsRequireOwnersAndHaveNoIndependentRespawn()
        {
            DungeonNamedLifecycleDefinition[] adds = DungeonNamedLifecycleCatalog.All()
                .Where(value => value.Kind == DungeonNamedDomainKind.OwnedAdd)
                .ToArray();
            Assert.AreEqual(2, adds.Length);
            Assert.IsTrue(adds.All(value => value.OwnerRequired));
            Assert.IsTrue(adds.All(
                value => value.Classification
                         == DungeonNamedRespawnClassification.ExplicitlyNoIndependentRespawn));
            Assert.IsTrue(adds.All(
                value => value.Trigger == DungeonNamedRespawnTrigger.OwnerAction));
        }

        [TestMethod]
        public void MurialDeathCancelsCombatAndPatrol()
        {
            string npcRuntime = Read(@"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs");
            Assert.IsTrue(npcRuntime.Contains("this.ordinaryEnemies.NotifyCharacterDied(target);"));
            Assert.IsTrue(npcRuntime.Contains("this.playfield.StopDyingNpcCombatState(target);"));
            Assert.IsTrue(npcRuntime.Contains("npcController.StopFollow();"));
            Assert.IsTrue(npcRuntime.Contains("NpcChaseInvalidationReason.Death"));
        }

        [TestMethod]
        public void MurialRespawnPolicyCreatesExactlyOneActor()
        {
            DungeonNamedLifecycleDefinition murial =
                DungeonNamedLifecycleCatalog.Get(DungeonNamedLifecycleCatalog.MurialProfileKey);
            Assert.AreEqual(DungeonNamedDomainKind.OrdinaryPatrol, murial.Kind);
            Assert.AreEqual(DungeonNamedRespawnTrigger.NpcDespawn, murial.Trigger);
            Assert.AreEqual(
                DungeonNamedRespawnClassification.ProvenSharedNamedRespawnRule,
                murial.Classification);
            Assert.IsTrue(murial.DelayRule.Contains("300 seconds"));

            string population = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\WorldPopulationController.cs");
            Assert.IsTrue(population.Contains("state.CurrentRuntimeIdentity.Instance != 0"));
            Assert.IsTrue(population.Contains("this.scheduler.Contains(spawnKey)"));
        }

        [TestMethod]
        public void MurialRespawnStartsExactlyOnePatrolWorker()
        {
            var provider = new CapturedTempleOfThreeWindsContentProvider();
            OrdinaryEnemySpawnDefinition murial = provider.GetSpawns().Single(
                value => value.ProfileKey == DungeonNamedLifecycleCatalog.MurialProfileKey);
            Assert.AreEqual(OrdinaryEnemyMovementMode.Patrol, murial.MovementMode);
            Assert.AreEqual(20, murial.Waypoints.Length);

            string ordinary = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs");
            Assert.IsTrue(ordinary.Contains("this.ApplyMovement(character, controller, spawn);"));
            Assert.IsTrue(ordinary.Contains(
                "this.activeRuntimeIdentityBySource[spawn.SourceIdentity] = character.Identity.Instance;"));
        }

        [TestMethod]
        public void MurialLiveReentryDoesNotDuplicateActorOrPatrol()
        {
            string population = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\WorldPopulationController.cs");
            Assert.IsTrue(population.Contains("state.CurrentRuntimeIdentity.Instance != 0) return false;"));
            Assert.IsTrue(population.Contains("this.scheduler.CancelPlayfield(playfieldId);"));
            string ordinary = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\OrdinaryEnemyRuntimeService.cs");
            Assert.IsTrue(ordinary.Contains(
                "this.activeRuntimeIdentityBySource.ContainsKey(spawn.SourceIdentity)"));
        }

        [TestMethod]
        public void EveryNamedDeathCreatesAtMostOneCorpse()
        {
            string playfield = Read(@"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            string inventory = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CorpseInventoryService.cs");
            Assert.IsTrue(playfield.Contains(
                "this.pendingCorpseSpawns.ContainsKey(target.Identity.Instance)"));
            Assert.IsTrue(playfield.Contains("this.corpseInventoryService.ContainsDeadNpc("));
            Assert.IsTrue(inventory.Contains("Duplicate corpse for dead NPC:"));
        }

        [TestMethod]
        public void EveryNamedDeathPerformsAtMostOneAtomicLootRoll()
        {
            string playfield = Read(@"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            Assert.AreEqual(
                1,
                CountOccurrences(
                    playfield,
                    "GlobalLootRuntimeService.Generate(target, this.Identity.Instance)"));
            Assert.IsTrue(playfield.IndexOf(
                "this.corpseInventoryService.ContainsDeadNpc(",
                StringComparison.Ordinal) < playfield.IndexOf(
                "GlobalLootRuntimeService.Generate(target, this.Identity.Instance)",
                StringComparison.Ordinal));
        }

        [TestMethod]
        public void CorpseReopenDoesNotRerollLoot()
        {
            string playfield = Read(@"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            Assert.AreEqual(
                1,
                CountOccurrences(
                    playfield,
                    "GlobalLootRuntimeService.Generate(target, this.Identity.Instance)"));
            Assert.IsTrue(playfield.Contains("this.corpseInventoryService.MarkOpened("));
            string inventory = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CorpseInventoryService.cs");
            Assert.IsTrue(inventory.Contains("internal CorpseLootItem[] EnumerateItems("));
        }

        [TestMethod]
        public void PlayfieldReentryDoesNotRerollExistingCorpseLoot()
        {
            string playfield = Read(@"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            Assert.IsTrue(playfield.Contains(
                "private readonly CorpseInventoryService corpseInventoryService"));
            Assert.IsTrue(playfield.Contains("this.corpseInventoryService.ContainsDeadNpc("));
            Assert.IsFalse(playfield.Contains("GlobalLootRuntimeService.GenerateDeterministic"));
        }

        [TestMethod]
        public void ReplacementRuntimeRetainsNoOldLifecycleOwnership()
        {
            string npcRuntime = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs");
            string playfield = Read(@"AORebirth\Server\ZoneEngine\Core\Playfields\Playfield.cs");
            Assert.IsTrue(npcRuntime.Contains("this.combatTick.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntime.Contains("this.worldPopulation.ClearPlayfield("));
            Assert.IsTrue(npcRuntime.Contains("this.capturedSubwayEncounters.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntime.Contains("this.capturedTempleEncounters.ClearRuntimeState();"));
            Assert.IsTrue(playfield.Contains("this.pendingCorpseSpawns.Clear();"));
            Assert.IsTrue(playfield.Contains("this.pendingCorpseCreditAwards.Clear();"));
            Assert.IsTrue(playfield.Contains("this.corpseInventoryService.ClearPlayfield("));
        }

        [TestMethod]
        public void Pf127AndPf1931SchedulesRemainIndependent()
        {
            var scheduler = new DungeonNamedRespawnScheduler();
            DateTime due = DateTime.UtcNow;
            Assert.IsTrue(scheduler.Schedule(
                127,
                DungeonNamedLifecycleCatalog.AbmouthProfileKey,
                DungeonNamedLifecycleCatalog.AbmouthProfileKey,
                due));
            Assert.IsTrue(scheduler.Schedule(
                1931,
                DungeonNamedLifecycleCatalog.UkleshProfileKey,
                DungeonNamedLifecycleCatalog.AzturProfileKey,
                due));
            scheduler.CancelPlayfield(127);
            Assert.AreEqual(1, scheduler.Count);
            Assert.IsFalse(scheduler.Contains(DungeonNamedLifecycleCatalog.AbmouthProfileKey));
            Assert.IsTrue(scheduler.Contains(DungeonNamedLifecycleCatalog.UkleshProfileKey));
        }

        [TestMethod]
        public void StrikeForemanLifecycleContractRemainsExact()
        {
            DungeonNamedLifecycleDefinition strike =
                DungeonNamedLifecycleCatalog.Get(DungeonNamedLifecycleCatalog.StrikeForemanProfileKey);
            Assert.AreEqual(127, strike.PlayfieldId);
            Assert.AreEqual(DungeonNamedRespawnTrigger.Death, strike.Trigger);
            Assert.IsTrue(strike.DelayRule.Contains("600 seconds"));
            Assert.AreEqual(
                DungeonNamedLootOwnership.GlobalAtomicCapturedDefinition,
                strike.LootOwnership);
        }

        [TestMethod]
        public void OrdinaryCombatCatalogRemainsFourHundredEightyNine()
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                new CapturedTempleOfThreeWindsContentProvider());
            Assert.AreEqual(322, catalog.GetSpawns().Count(value => value.PlayfieldInstance == 127));
            Assert.AreEqual(167, catalog.GetSpawns().Count(value => value.PlayfieldInstance == 1931));
        }

        [TestMethod]
        public void NamedCombatDomainsRemainNineteenOfNineteen()
        {
            Assert.AreEqual(19, DungeonNamedLifecycleCatalog.All().Length);
            Assert.AreEqual(
                14,
                DungeonNamedLifecycleCatalog.All().Count(
                    value => value.Kind == DungeonNamedDomainKind.Initial));
            Assert.AreEqual(
                2,
                DungeonNamedLifecycleCatalog.All().Count(
                    value => value.Kind == DungeonNamedDomainKind.Successor));
            Assert.AreEqual(
                2,
                DungeonNamedLifecycleCatalog.All().Count(
                    value => value.Kind == DungeonNamedDomainKind.OwnedAdd));
            Assert.AreEqual(
                1,
                DungeonNamedLifecycleCatalog.All().Count(
                    value => value.Kind == DungeonNamedDomainKind.OrdinaryPatrol));
        }

        [TestMethod]
        public void MissionLifecycleOwnershipIsNotUsedByDungeonCatalog()
        {
            Assert.IsFalse(DungeonNamedLifecycleCatalog.All().Any(
                value => value.ProfileKey.IndexOf(
                    "mission",
                    StringComparison.OrdinalIgnoreCase) >= 0));
            string lifecycle = Read(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\DungeonNamedLifecycle.cs");
            Assert.IsFalse(lifecycle.Contains("ZoneEngine.Core.Missions"));
            Assert.IsFalse(lifecycle.Contains("MissionAcg"));
        }

        private static string Read(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        }

        private static int CountOccurrences(string value, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static string FindRepositoryRoot([CallerFilePath] string sourcePath = null)
        {
            DirectoryInfo directory = new FileInfo(sourcePath).Directory;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "AORebirth"))
                    && File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Fail("Could not find repository root.");
            return string.Empty;
        }
    }
}
