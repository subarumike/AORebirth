namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionAcgOperationalRuntimeTests
    {
        private MissionAcgLayoutCatalog catalog;
        private string temporaryRoot;

        [TestInitialize]
        public void Initialize()
        {
            this.catalog = MissionAcgLegacyLayoutCatalogFactory.Create();
            this.temporaryRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "aorebirth-acg-stage5-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.temporaryRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.temporaryRoot))
            {
                Directory.Delete(this.temporaryRoot, true);
            }
        }

        [TestMethod]
        public void OperationalFormatIsVersionOneAndSeparateFromEarlierFormats()
        {
            Assert.AreEqual(1, MissionAcgOperationalState.CurrentFormatVersion);
            Assert.AreEqual(1, MissionAcgRuntimeState.CurrentFormatVersion);
            Assert.AreEqual(2, MissionAcgInstanceBinding.CurrentFormatVersion);
        }

        [TestMethod]
        public void NpcRuntimeIdentityRoundTripsWithoutCapturedPfLeakage()
        {
            MissionAcgBindingRecord record = this.CreateBinding(1, this.FirstPf());
            MissionAcgOperationalState initial = this.CreateState(record, false, false);
            MissionAcgOperationalState restored = this.RoundTrip(record, initial);
            Assert.AreEqual(initial.Npcs[0].RuntimeIdentity, restored.Npcs[0].RuntimeIdentity);
            Assert.AreEqual(record.Binding.AllocatedLivePlayfield2, restored.AllocatedLivePlayfield2);
            Assert.AreNotEqual(
                this.catalog.FindByLayoutId(record.Binding.SelectedBundleId).SourcePlayfield2,
                restored.AllocatedLivePlayfield2);
        }

        [TestMethod]
        public void SameCapturedSlotInTwoPf2InstancesHasDifferentRuntimeIdentity()
        {
            int firstPf = this.FirstPf();
            int secondPf = firstPf + 1;
            int firstRuntime = RuntimeIdentity(firstPf, 1);
            int secondRuntime = RuntimeIdentity(secondPf, 1);
            Assert.AreNotEqual(firstRuntime, secondRuntime);
            Assert.AreEqual(1, firstRuntime & 0xFF);
            Assert.AreEqual(1, secondRuntime & 0xFF);
        }

        [TestMethod]
        public void DeadNpcAndCorpseOwnershipSurviveRestart()
        {
            MissionAcgBindingRecord record = this.CreateBinding(2, this.FirstPf());
            MissionAcgOperationalState restored =
                this.RoundTrip(record, this.CreateState(record, true, false));
            Assert.AreEqual(MissionAcgNpcLifeState.Dead, restored.Npcs[0].LifeState);
            Assert.AreEqual(0, restored.Npcs[0].CurrentHealth);
            Assert.AreEqual(MissionAcgCorpseState.Available, restored.Npcs[0].CorpseState);
            Assert.AreEqual(
                restored.Npcs[0].RuntimeIdentity.Instance,
                restored.Npcs[0].CorpseIdentity.Instance);
        }

        [TestMethod]
        public void CorpseIdentityIsIsolatedBetweenSimultaneousInstances()
        {
            int firstPf = this.FirstPf();
            MissionAcgBindingRecord first = this.CreateBinding(3, firstPf);
            MissionAcgBindingRecord second = this.CreateBinding(4, firstPf + 1);
            MissionAcgOperationalState firstState = this.CreateState(first, true, false);
            MissionAcgOperationalState secondState = this.CreateState(second, true, false);
            Assert.AreNotEqual(
                firstState.Npcs[0].CorpseIdentity.Instance,
                secondState.Npcs[0].CorpseIdentity.Instance);
        }

        [TestMethod]
        public void UnresolvedChestIsExplicitlyEmptyAndCannotRefillOnRestart()
        {
            MissionAcgBindingRecord record = this.CreateBinding(5, this.FirstPf());
            MissionAcgOperationalState restored =
                this.RoundTrip(record, this.CreateState(record, false, true));
            Assert.AreEqual(
                MissionAcgLootAuthority.UnresolvedEmpty,
                restored.Chests[0].LootAuthority);
            Assert.IsTrue(restored.Chests[0].IsOpen);
            Assert.IsTrue(restored.Chests[0].IsExhausted);
            Assert.AreEqual(0, restored.Chests[0].TransferredItemCount);
        }

        [TestMethod]
        public void AtomicReplacementPreservesOnlyLatestDurableNpcState()
        {
            MissionAcgBindingRecord record = this.CreateBinding(6, this.FirstPf());
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            MissionAcgOperationalState alive = this.CreateState(record, false, false);
            string failure;
            Assert.IsTrue(store.TryWrite(alive, false, out failure), failure);
            MissionAcgOperationalState dead = this.CreateState(record, true, false);
            Assert.IsTrue(store.TryWrite(dead, true, out failure), failure);
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsTrue(
                store.TryLoad(record.Binding, out restored, out exists, out failure),
                failure);
            Assert.IsTrue(exists);
            Assert.AreEqual(MissionAcgNpcLifeState.Dead, restored.Npcs[0].LifeState);
            Assert.IsFalse(File.Exists(store.PathFor(record.Binding.AcceptedQuestIdentity) + ".bak"));
        }

        [TestMethod]
        public void TamperedOperationalSidecarFailsClosed()
        {
            MissionAcgBindingRecord record = this.CreateBinding(7, this.FirstPf());
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(record, false, false), false, out failure),
                failure);
            string path = store.PathFor(record.Binding.AcceptedQuestIdentity);
            File.AppendAllText(path, "tamper=1\r\n");
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsFalse(store.TryLoad(record.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void TruncatedOperationalSidecarFailsClosed()
        {
            MissionAcgBindingRecord record = this.CreateBinding(8, this.FirstPf());
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(store.PathFor(record.Binding.AcceptedQuestIdentity)));
            File.WriteAllText(
                store.PathFor(record.Binding.AcceptedQuestIdentity),
                "AORebirth.MissionAcgOperationalState\r\n");
            MissionAcgOperationalState restored;
            bool exists;
            string failure;
            Assert.IsFalse(store.TryLoad(record.Binding, out restored, out exists, out failure));
            StringAssert.Contains(failure, "truncated");
        }

        [TestMethod]
        public void UnknownOperationalFormatFailsClosed()
        {
            MissionAcgBindingRecord record = this.CreateBinding(9, this.FirstPf());
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(record, false, false), false, out failure),
                failure);
            string path = store.PathFor(record.Binding.AcceptedQuestIdentity);
            string contents = File.ReadAllText(path).Replace("FormatVersion=1", "FormatVersion=99");
            File.WriteAllText(path, contents);
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsFalse(store.TryLoad(record.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void BindingMismatchCannotRedirectOperationalState()
        {
            MissionAcgBindingRecord first = this.CreateBinding(10, this.FirstPf());
            MissionAcgBindingRecord second = this.CreateBinding(11, this.FirstPf() + 1);
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(
                store.TryWrite(this.CreateState(first, false, false), false, out failure),
                failure);
            string firstPath = store.PathFor(first.Binding.AcceptedQuestIdentity);
            string secondPath = store.PathFor(second.Binding.AcceptedQuestIdentity);
            Directory.CreateDirectory(Path.GetDirectoryName(secondPath));
            File.Copy(firstPath, secondPath);
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsFalse(store.TryLoad(second.Binding, out restored, out exists, out failure));
        }

        [TestMethod]
        public void DuplicateNpcRuntimeIdentityIsRejected()
        {
            MissionAcgBindingRecord record = this.CreateBinding(12, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, false, false);
            bool rejected = false;
            try
            {
                new MissionAcgOperationalState(
                    MissionAcgOperationalState.CurrentFormatVersion,
                    state.AcceptedQuestIdentity,
                    state.OwnerIdentity,
                    state.AllocatedLivePlayfield2,
                    state.BundleId,
                    state.BundlePayloadSha256,
                    state.BuildingIdentity,
                    new[] { state.Npcs[0], state.Npcs[0] },
                    state.Chests,
                    MissionAcgOperationalCleanupState.Active,
                    DateTime.UtcNow);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Assert.IsTrue(rejected);
        }

        [TestMethod]
        public void CleanupIsExactAndIdempotentAtTheStateBoundary()
        {
            MissionAcgBindingRecord record = this.CreateBinding(13, this.FirstPf());
            MissionAcgOperationalState state = this.CreateState(record, false, false);
            MissionAcgOperationalState pending = state.BeginCleanup(DateTime.UtcNow);
            MissionAcgOperationalState completed = pending.CompleteCleanup(DateTime.UtcNow);
            MissionAcgOperationalState repeated = completed.CompleteCleanup(DateTime.UtcNow);
            Assert.AreEqual(MissionAcgOperationalCleanupState.Completed, repeated.CleanupState);
            Assert.IsTrue(repeated.Npcs.All(x => x.CleanupCompleted));
            Assert.IsTrue(repeated.Chests.All(x => x.CleanupCompleted));
        }

        [TestMethod]
        public void AllSelectableBundlesHaveFiniteCapturedNpcCoordinatesAndAttributes()
        {
            Assert.AreEqual(5, this.catalog.SelectableLayouts.Count);
            foreach (MissionAcgLayoutBundle bundle in this.catalog.SelectableLayouts)
            {
                Assert.IsTrue(MissionAcgSpatialValidator.IsFinite(bundle.EntryPoint));
                Assert.IsTrue(MissionAcgSpatialValidator.IsFinite(bundle.Exit.Position));
                Assert.IsTrue(bundle.NpcSlots.Count > 0);
                foreach (MissionAcgNpcSlotRecord npc in bundle.NpcSlots)
                {
                    Assert.IsTrue(MissionAcgSpatialValidator.IsFinite(npc.Position));
                    Assert.IsTrue(MissionAcgSpatialValidator.IsFinite(npc.Heading));
                    Assert.IsTrue(npc.TemplateId > 0);
                    Assert.IsTrue(npc.MonsterData > 0);
                    Assert.IsTrue(npc.CapturedLevel > 0);
                    Assert.IsTrue(npc.CapturedHealth > 0);
                }
            }
        }

        [TestMethod]
        public void SharedAndCapturedPf2ValuesRemainBlockedForLiveAllocation()
        {
            Assert.IsFalse(
                MissionAcgAllocationService.IsAllocatableRange(
                    MissionAcgAllocationService.LegacySharedPlayfield2));
            foreach (MissionAcgLayoutBundle bundle in this.catalog.SelectableLayouts)
            {
                Assert.AreNotEqual(
                    bundle.SourcePlayfield2,
                    this.CreateBinding(20 + bundle.SourcePlayfield2, this.FirstPf()).Binding
                        .AllocatedLivePlayfield2);
            }
        }

        [TestMethod]
        public void IncompleteShape1441804RemainsExcluded()
        {
            MissionAcgLayoutBundle incomplete = this.catalog.FindBySourcePlayfield2(1441804);
            Assert.IsTrue(incomplete == null || !incomplete.IsSelectable);
            Assert.IsFalse(
                this.catalog.SelectableLayouts.Any(x => x.SourcePlayfield2 == 1441804));
        }

        private MissionAcgOperationalState RoundTrip(
            MissionAcgBindingRecord record,
            MissionAcgOperationalState state)
        {
            var store = new MissionAcgOperationalStateStore(this.temporaryRoot);
            string failure;
            Assert.IsTrue(store.TryWrite(state, false, out failure), failure);
            MissionAcgOperationalState restored;
            bool exists;
            Assert.IsTrue(
                store.TryLoad(record.Binding, out restored, out exists, out failure),
                failure);
            Assert.IsTrue(exists);
            return restored;
        }

        private MissionAcgOperationalState CreateState(
            MissionAcgBindingRecord record,
            bool dead,
            bool openedChest)
        {
            int runtimeInstance =
                RuntimeIdentity(record.Binding.AllocatedLivePlayfield2, 1);
            MissionAcgIdentityRecord runtimeNpc =
                new MissionAcgIdentityRecord(0xC350, runtimeInstance);
            MissionAcgIdentityRecord corpse =
                dead ? new MissionAcgIdentityRecord(0xC76A, runtimeInstance) : null;
            var npc =
                new MissionAcgNpcRuntimeState(
                    0,
                    new MissionAcgIdentityRecord(0xC350, 700001),
                    runtimeNpc,
                    new MissionAcgPointRecord(10.0f, 5.0f, 20.0f),
                    new MissionAcgRotationRecord(0.0f, 0.0f, 0.0f, 1.0f),
                    30369,
                    30369,
                    42,
                    1773,
                    dead ? 0 : 1773,
                    104,
                    null,
                    "Captured Mission NPC",
                    MissionAcgNpcRole.KillTarget,
                    dead ? MissionAcgNpcLifeState.Dead : MissionAcgNpcLifeState.Alive,
                    dead ? MissionAcgNpcCombatState.Dead : MissionAcgNpcCombatState.Stationary,
                    corpse,
                    dead ? MissionAcgCorpseState.Available : MissionAcgCorpseState.None,
                    1,
                    false);
            var chest =
                new MissionAcgChestRuntimeState(
                    0,
                    new MissionAcgIdentityRecord(0xC74F, 800001),
                    new MissionAcgIdentityRecord(
                        0xC74F,
                        RuntimeIdentity(record.Binding.AllocatedLivePlayfield2, 2)),
                    MissionAcgLootAuthority.UnresolvedEmpty,
                    openedChest,
                    openedChest,
                    0,
                    false);
            return new MissionAcgOperationalState(
                MissionAcgOperationalState.CurrentFormatVersion,
                record.Binding.AcceptedQuestIdentity,
                record.Binding.OwnerIdentity,
                record.Binding.AllocatedLivePlayfield2,
                record.Binding.SelectedBundleId,
                record.Binding.SelectedBundlePayloadSha256,
                record.Binding.AcgBuildingIdentity,
                new[] { npc },
                new[] { chest },
                MissionAcgOperationalCleanupState.Active,
                new DateTime(2026, 7, 28, 18, 0, 0, DateTimeKind.Utc));
        }

        private MissionAcgBindingRecord CreateBinding(int salt, int livePf)
        {
            var owner = new MissionAcgIdentityRecord(0xC350, 10000 + salt);
            MissionAcgLayoutBundle bundle =
                MissionAcgLayoutSelector.Select(
                    this.catalog,
                    new MissionAcgSelectionInput(
                        2000 + salt,
                        MissionRollType.KillPerson,
                        42,
                        owner));
            DateTime accepted =
                new DateTime(2026, 7, 28, 17, 0, 0, DateTimeKind.Utc).AddSeconds(salt);
            MissionAcgInstanceBinding binding =
                MissionAcgInstanceBinding.CreateDurable(
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.AcceptedQuestIdentityType,
                        0x50000000 + salt),
                    new MissionAcgIdentityRecord(0xDAC3, 0x01000000 + salt),
                    owner,
                    null,
                    MissionRollType.KillPerson,
                    42,
                    2000 + salt,
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.MissionKeyIdentityType,
                        0x60000000 + salt),
                    new MissionAcgIdentityRecord(0x9C50, 710),
                    43308,
                    27595,
                    229.605f,
                    6.504f,
                    452.042f,
                    new MissionAcgIdentityRecord(0xDAC1, 0x1000 + salt),
                    bundle,
                    livePf,
                    accepted,
                    accepted.AddHours(48));
            return new MissionAcgBindingRecord(
                binding,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Active,
                    MissionAcgCleanupState.None,
                    accepted,
                    null),
                string.Empty);
        }

        private int FirstPf()
        {
            int value = MissionAcgAllocationService.MinimumLivePlayfield2;
            while (value == MissionAcgAllocationService.LegacySharedPlayfield2
                   || this.catalog.FindBySourcePlayfield2(value) != null)
            {
                value++;
            }

            return value;
        }

        private static int RuntimeIdentity(int livePf, int ordinal)
        {
            return unchecked((int)0x60000000)
                   | ((livePf - MissionAcgAllocationService.MinimumLivePlayfield2) << 8)
                   | ordinal;
        }
    }
}
