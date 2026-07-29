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
    public class DungeonNamedEncounterCompletionTests
    {
        [TestMethod]
        public void AuthoritativeInventoryEnumeratesEveryNamedStageAndOwnedAddExactlyOnce()
        {
            Entry[] entries = Inventory();

            Assert.AreEqual(18, entries.Length);
            Assert.AreEqual(entries.Length, entries.Select(value => value.ProfileKey).Distinct().Count());
            Assert.AreEqual(4, entries.Count(value => value.Playfield == 127));
            Assert.AreEqual(14, entries.Count(value => value.Playfield == 1931));
            Assert.AreEqual(13, entries.Count(value => value.Kind == "initial"));
            Assert.AreEqual(2, entries.Count(value => value.Kind == "successor"));
            Assert.AreEqual(2, entries.Count(value => value.Kind == "add"));
            Assert.AreEqual(1, entries.Count(value => value.Kind == "ordinary-patrol"));
            Assert.AreEqual(
                1,
                entries.Count(
                    value => value.ProfileKey
                             == CapturedTempleOfThreeWindsContentProvider.MurialProfileKey));
            Assert.AreEqual(
                1,
                entries.Count(
                    value => value.ProfileKey
                             == "totw.647.encounter.re-animator.reanimated-corpse"));
        }

        [TestMethod]
        public void EveryInventoryEntryHasAnExactReadyCombatDomain()
        {
            foreach (Entry entry in Inventory())
            {
                if (entry.Playfield == 1931)
                {
                    Assert.IsNotNull(entry.Combat, entry.ProfileKey);
                    Assert.AreNotEqual(
                        CapturedEnemyAttackModel.Unresolved,
                        entry.Combat.AttackModel,
                        entry.ProfileKey);
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(entry.Combat.Evidence),
                        entry.ProfileKey);
                }
            }

            string subwayCombat = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedEnemyCombatContract.cs"));
            Assert.IsTrue(subwayCombat.Contains("case 155962:"));
            Assert.IsTrue(subwayCombat.Contains("case 203748:"));
            Assert.IsTrue(subwayCombat.Contains("case 203726:"));
            Assert.IsTrue(subwayCombat.Contains("case 31909:"));
            Assert.AreNotEqual("totw.1931.boss.uklesh-the-frozen", "totw.1931.boss.khalum");
            Assert.AreNotEqual("totw.1931.boss.khalum", "totw.1931.boss.aztur-the-immortal");
        }

        [TestMethod]
        public void SuccessorAndAddDomainsAreOwnedByTheirEncounterStateMachines()
        {
            string root = FindRepositoryRoot();
            string temple = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));
            string subway = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayEncounterRuntimeService.cs"));
            Assert.IsTrue(temple.Contains("this.RequestNextReanimation(pending.FinishAtUtc);"));
            Assert.IsTrue(temple.Contains("return this.DetachLivingReanimatedAdds();"));
            Assert.IsTrue(temple.Contains("suppressIndependentRespawn: true"));
            Assert.IsTrue(temple.Contains("successorProfileKey = KhalumProfileKey;"));
            Assert.IsTrue(temple.Contains("delaySeconds = KhalumSpawnAfterUkleshDeathSeconds;"));
            Assert.IsTrue(temple.Contains("successorProfileKey = AzturProfileKey;"));
            Assert.IsTrue(temple.Contains("delaySeconds = AzturSpawnAfterKhalumDeathSeconds;"));
            Assert.IsTrue(subway.Contains("slot.Generation++;"));
            Assert.IsTrue(subway.Contains("summon.Stats[StatIds.petmaster].Value = 0;"));
        }

        [TestMethod]
        public void EncounterRegistryRetirementIsPlayfieldOwnedAndIndependent()
        {
            string root = FindRepositoryRoot();
            string subway = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayEncounterRuntimeService.cs"));
            string temple = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));

            Assert.IsTrue(subway.Contains("new RegisteredEncounterDefinition(playfieldInstance, definition)"));
            Assert.IsTrue(subway.Contains("value.Value.PlayfieldInstance == playfieldInstance"));
            Assert.IsFalse(subway.Contains("playfieldInstance != CapturedSubwayEncounterRuntimeService.SubwayPlayfieldId"));
            Assert.IsTrue(temple.Contains("this.playfield.Identity.Instance,"));
            Assert.IsTrue(temple.Contains("CapturedEncounterRuntimeRegistry.RemoveForPlayfield("));
        }

        [TestMethod]
        public void RuntimeDisposalCancelsNamedCombatMovementRespawnAndVisibilityOwnership()
        {
            string root = FindRepositoryRoot();
            string npcRuntime = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\NPCRuntimeService.cs"));
            string temple = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedTempleOfThreeWindsEncounterRuntimeService.cs"));
            string subway = File.ReadAllText(
                Path.Combine(
                    root,
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedSubwayEncounterRuntimeService.cs"));

            Assert.IsTrue(npcRuntime.Contains("character.DoNotDoTimers = true;"));
            Assert.IsTrue(npcRuntime.Contains("controller.StopFollow();"));
            Assert.IsTrue(npcRuntime.Contains("this.combatTick.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntime.Contains("this.corpseLifecycle.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntime.Contains("this.capturedSubwayEncounters.ClearRuntimeState();"));
            Assert.IsTrue(npcRuntime.Contains("this.capturedTempleEncounters.ClearRuntimeState();"));
            Assert.IsTrue(temple.Contains("CapturedEncounterRuntimeRegistry.RemoveForPlayfield("));
            Assert.IsTrue(temple.Contains("state.ResetAll();"));
            Assert.IsTrue(temple.Contains("slot.Reset();"));
            Assert.IsTrue(subway.Contains("CapturedEncounterRuntimeRegistry.RemoveForPlayfield("));
            Assert.IsTrue(subway.Contains("slot.SpawnDueAtUtc = null;"));
        }

        [TestMethod]
        public void OrdinaryDungeonBaselineRemainsLockedAtFourHundredEightyNine()
        {
            var catalog =
                new OrdinaryEnemyCatalog(
                    new CapturedSubwayContentProvider(),
                    new CapturedSubwayOrdinaryContentProvider(),
                    new CapturedTempleOfThreeWindsContentProvider());
            Assert.AreEqual(322, catalog.GetSpawns().Count(value => value.PlayfieldInstance == 127));
            Assert.AreEqual(167, catalog.GetSpawns().Count(value => value.PlayfieldInstance == 1931));
            Assert.AreEqual(489, catalog.GetSpawns().Count(value => value.PlayfieldInstance == 127
                                                                   || value.PlayfieldInstance == 1931));
        }

        private static Entry[] Inventory()
        {
            return new[]
            {
                new Entry(127, "subway.127.boss.abmouth-supremus", "initial",
                    null),
                new Entry(127, "subway.127.boss.vergil-aeneid", "initial",
                    null),
                new Entry(127, "subway.127.named.eumenides", "initial",
                    null),
                new Entry(127, "subway.127.encounter.abmouth-infector", "add",
                    null),
                new Entry(1931, "totw.647.boss.defender-of-the-three", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.DefenderOfTheThree()),
                new Entry(1931, "totw.647.named.windcaller-yatila", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.WindcallerYatila()),
                new Entry(1931, "totw.647.named.reverend-gulard", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.ReverendGulard()),
                new Entry(1931, "totw.647.boss.the-re-animator", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.ReAnimator()),
                new Entry(1931, "totw.647.named.acolyte-betany", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.AcolyteBetany()),
                new Entry(1931, "totw.647.boss.the-curator", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.TheCurator()),
                new Entry(1931, "totw.647.boss.nematet-the-custodian-of-time", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.NematetTheCustodianOfTime()),
                new Entry(1931, "totw.1931.boss.guardian-of-tomorrow", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.GuardianOfTomorrow()),
                new Entry(1931, "totw.1931.boss.gartua-the-doorkeeper", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.GartuaTheDoorkeeper()),
                new Entry(1931, "totw.1931.boss.uklesh-the-frozen", "initial",
                    CapturedTempleOfThreeWindsCombatCatalog.UkleshTheFrozen()),
                new Entry(1931, "totw.1931.boss.khalum", "successor",
                    CapturedTempleOfThreeWindsCombatCatalog.Khalum()),
                new Entry(1931, "totw.1931.boss.aztur-the-immortal", "successor",
                    CapturedTempleOfThreeWindsCombatCatalog.AzturTheImmortal()),
                new Entry(1931, "totw.647.encounter.re-animator.reanimated-corpse", "add",
                    CapturedTempleOfThreeWindsCombatCatalog.ReanimatedCorpse()),
                new Entry(1931, CapturedTempleOfThreeWindsContentProvider.MurialProfileKey, "ordinary-patrol",
                    CapturedTempleOfThreeWindsCombatCatalog.MurialTheFaithful())
            };
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

        private sealed class Entry
        {
            internal Entry(
                int playfield,
                string profileKey,
                string kind,
                CapturedEnemyCombatContract combat)
            {
                this.Playfield = playfield;
                this.ProfileKey = profileKey;
                this.Kind = kind;
                this.Combat = combat;
            }

            internal int Playfield { get; private set; }
            internal string ProfileKey { get; private set; }
            internal string Kind { get; private set; }
            internal CapturedEnemyCombatContract Combat { get; private set; }
        }
    }
}
