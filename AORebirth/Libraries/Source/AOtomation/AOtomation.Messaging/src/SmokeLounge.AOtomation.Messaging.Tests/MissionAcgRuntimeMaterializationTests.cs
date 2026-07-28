namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionAcgRuntimeMaterializationTests
    {
        private MissionAcgLayoutCatalog catalog;
        private string temporaryRoot;

        [TestInitialize]
        public void Initialize()
        {
            this.catalog = MissionAcgLegacyLayoutCatalogFactory.Create();
            this.temporaryRoot =
                Path.Combine(Path.GetTempPath(), "aorebirth-acg-stage3-" + Guid.NewGuid().ToString("N"));
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
        public void ExactBundleMaterializesPayloadBuildingPfSpawnExitAndAllFiveObjectives()
        {
            MissionRollType[] types =
            {
                MissionRollType.KillPerson,
                MissionRollType.FindItemReturn,
                MissionRollType.FindItem,
                MissionRollType.RepairMachine,
                MissionRollType.FindPerson
            };
            for (int i = 0; i < types.Length; i++)
            {
                MissionAcgBindingRecord record = this.CreateRecord(types[i], i + 1, 100 + i, this.FirstPf() + i);
                MissionAcgLayoutBundle bundle = this.catalog.FindByLayoutId(record.Binding.SelectedBundleId);
                MissionAcgMaterializedInstance instance = Materialize(record, bundle, null);
                Assert.AreSame(bundle, instance.Bundle);
                Assert.AreEqual(record.Binding.AcgBuildingIdentity, instance.State.BuildingIdentity);
                Assert.AreEqual(record.Binding.AllocatedLivePlayfield2, instance.State.AllocatedLivePlayfield2);
                Assert.AreSame(bundle.EntryPoint, instance.Spawn);
                Assert.AreSame(bundle.Exit, instance.Exit);
                CollectionAssert.AreEqual(bundle.CopyGeneratorPayload(), instance.Bundle.CopyGeneratorPayload());
                Assert.IsTrue(instance.Objects.Any(x => x.Identity.Kind == MissionAcgRuntimeObjectKind.Exit));
                Assert.IsTrue(instance.Objects.Any(x => x.Identity.Kind == MissionAcgRuntimeObjectKind.Door));
                Assert.IsTrue(instance.Objects.Any(x => x.Identity.Kind == MissionAcgRuntimeObjectKind.Chest));
                Assert.IsTrue(
                    instance.Objects.Any(
                        x => x.Identity.Kind == MissionAcgRuntimeObjectKind.StaticObjective
                             || x.Identity.Kind == MissionAcgRuntimeObjectKind.ObjectiveNpc
                             || x.Identity.Kind == MissionAcgRuntimeObjectKind.RepairMachine));
            }
        }

        [TestMethod]
        public void IdentitiesAreStableReversibleCollisionFreeAndPacketsUseOnlyLivePf()
        {
            int firstPf = this.FirstPf();
            MissionAcgBindingRecord first = this.CreateRecord(MissionRollType.FindItem, 10, 200, firstPf);
            MissionAcgBindingRecord second = this.CreateRecord(MissionRollType.FindItem, 11, 200, this.NextPf(firstPf));
            MissionAcgMaterializedInstance one =
                Materialize(first, this.catalog.FindByLayoutId(first.Binding.SelectedBundleId), null);
            MissionAcgMaterializedInstance again =
                Materialize(first, this.catalog.FindByLayoutId(first.Binding.SelectedBundleId), null);
            MissionAcgMaterializedInstance two =
                Materialize(second, this.catalog.FindByLayoutId(second.Binding.SelectedBundleId), null);
            var firstIds = new HashSet<int>();
            var secondIds = new HashSet<int>();
            for (int i = 0; i < one.State.IdentityEntries.Count; i++)
            {
                MissionAcgRuntimeIdentityEntry entry = one.State.IdentityEntries[i];
                Assert.AreEqual(entry.RuntimeIdentity, again.State.IdentityEntries[i].RuntimeIdentity);
                Assert.IsTrue(firstIds.Add(entry.RuntimeIdentity.Instance));
                int reversedPf;
                int ordinal;
                Assert.IsTrue(
                    MissionAcgRuntimeMaterializer.TryReverseRuntimeInstance(
                        entry.RuntimeIdentity.Instance,
                        out reversedPf,
                        out ordinal));
                Assert.AreEqual(firstPf, reversedPf);
                Assert.AreEqual(i + 1, ordinal);
            }

            foreach (MissionAcgRuntimeIdentityEntry entry in two.State.IdentityEntries)
            {
                secondIds.Add(entry.RuntimeIdentity.Instance);
            }

            Assert.AreEqual(0, firstIds.Intersect(secondIds).Count());
            foreach (MissionAcgRuntimeObject runtimeObject in one.Objects)
            {
                if (!runtimeObject.HasPacket)
                {
                    continue;
                }

                byte[] packet = runtimeObject.CopyPacket();
                Assert.IsTrue(Contains(packet, first.Binding.AllocatedLivePlayfield2));
                Assert.IsFalse(Contains(packet, one.Bundle.SourcePlayfield2));
                Assert.IsFalse(Contains(packet, runtimeObject.Identity.CapturedIdentity.Instance));
            }
        }

        [TestMethod]
        public void RegistryRequiresOwnerPfAndRuntimeIdentityAndCleanupIsExact()
        {
            int firstPf = this.FirstPf();
            MissionAcgBindingRecord first = this.CreateRecord(MissionRollType.FindItem, 20, 300, firstPf);
            MissionAcgBindingRecord second = this.CreateRecord(MissionRollType.FindItem, 21, 301, this.NextPf(firstPf));
            MissionAcgMaterializedInstance one =
                Materialize(first, this.catalog.FindByLayoutId(first.Binding.SelectedBundleId), null);
            MissionAcgMaterializedInstance two =
                Materialize(second, this.catalog.FindByLayoutId(second.Binding.SelectedBundleId), null);
            var registry = new MissionAcgRuntimeRegistry();
            string failure;
            Assert.IsTrue(registry.TryAdd(one, out failure), failure);
            Assert.IsTrue(registry.TryAdd(two, out failure), failure);
            MissionAcgRuntimeObject door =
                one.Objects.First(x => x.Identity.Kind == MissionAcgRuntimeObjectKind.Door);
            MissionAcgMaterializedInstance resolvedInstance;
            MissionAcgRuntimeObject resolvedObject;
            DateTime now = first.Binding.AcceptedUtc.AddMinutes(1);
            Assert.IsTrue(
                registry.TryResolveObject(
                    300,
                    firstPf,
                    door.Identity.RuntimeIdentity.Type,
                    door.Identity.RuntimeIdentity.Instance,
                    now,
                    out resolvedInstance,
                    out resolvedObject));
            Assert.IsFalse(
                registry.TryResolveObject(
                    301,
                    firstPf,
                    door.Identity.RuntimeIdentity.Type,
                    door.Identity.RuntimeIdentity.Instance,
                    now,
                    out resolvedInstance,
                    out resolvedObject));
            Assert.IsTrue(registry.Remove(first.Binding.AcceptedQuestIdentity.Instance, firstPf));
            Assert.IsTrue(registry.TryGetByPlayfield(second.Binding.AllocatedLivePlayfield2, out resolvedInstance));
            Assert.AreSame(two, resolvedInstance);
        }

        [TestMethod]
        public void DoorChestAndLockStateSurviveAtomicRestartRestoration()
        {
            MissionAcgBindingRecord record = this.CreateRecord(MissionRollType.FindItem, 30, 400, this.FirstPf());
            MissionAcgLayoutBundle bundle = this.catalog.FindByLayoutId(record.Binding.SelectedBundleId);
            MissionAcgMaterializedInstance instance = Materialize(record, bundle, null);
            MissionAcgRuntimeDoorState openDoor = instance.State.DoorStates.First();
            MissionAcgRuntimeDoorState lockedDoor = instance.State.DoorStates.Skip(1).First();
            MissionAcgRuntimeChestState chest = instance.State.ChestStates.First();
            openDoor.Toggle();
            lockedDoor.SetLocked(true);
            lockedDoor.Toggle();
            chest.Open();
            instance.State.Touch(DateTime.UtcNow);
            var store =
                new MissionAcgRuntimeStateStore(Path.Combine(this.temporaryRoot, "mission-state"));
            string failure;
            Assert.IsTrue(store.TryWrite(instance.State, false, out failure), failure);
            MissionAcgRuntimeState restored;
            bool exists;
            Assert.IsTrue(store.TryLoad(record.Binding, bundle, out restored, out exists, out failure), failure);
            Assert.IsTrue(exists);
            MissionAcgRuntimeDoorState restoredOpen;
            MissionAcgRuntimeDoorState restoredLocked;
            MissionAcgRuntimeChestState restoredChest;
            Assert.IsTrue(restored.TryGetDoor(openDoor.RuntimeInstance, out restoredOpen));
            Assert.IsTrue(restored.TryGetDoor(lockedDoor.RuntimeInstance, out restoredLocked));
            Assert.IsTrue(restored.TryGetChest(chest.RuntimeInstance, out restoredChest));
            Assert.IsTrue(restoredOpen.IsOpen);
            Assert.IsTrue(restoredLocked.IsLocked);
            Assert.IsFalse(restoredLocked.IsOpen);
            Assert.IsTrue(restoredChest.IsOpen);
            MissionAcgMaterializedInstance rematerialized = Materialize(record, bundle, restored);
            Assert.AreEqual(
                instance.State.IdentityEntries[0].RuntimeIdentity,
                rematerialized.State.IdentityEntries[0].RuntimeIdentity);
        }

        [TestMethod]
        public void RuntimeStateIgnoresTemporaryWritesAndTruncationFailsClosed()
        {
            MissionAcgBindingRecord record =
                this.CreateRecord(MissionRollType.FindItem, 35, 450, this.FirstPf());
            MissionAcgLayoutBundle bundle =
                this.catalog.FindByLayoutId(record.Binding.SelectedBundleId);
            MissionAcgMaterializedInstance instance = Materialize(record, bundle, null);
            var store =
                new MissionAcgRuntimeStateStore(
                    Path.Combine(this.temporaryRoot, "mission-state"));
            string failure;
            Assert.IsTrue(store.TryWrite(instance.State, false, out failure), failure);
            string path = Directory.GetFiles(store.DirectoryPath, "*.state")[0];
            File.WriteAllText(path + ".partial.tmp", "partial");
            MissionAcgRuntimeState restored;
            bool exists;
            Assert.IsTrue(
                store.TryLoad(record.Binding, bundle, out restored, out exists, out failure),
                failure);
            Assert.IsTrue(exists);
            File.WriteAllText(
                path,
                "AORebirth-MissionAcgRuntimeState\r\nFormatVersion=1\r\n");
            Assert.IsFalse(
                store.TryLoad(record.Binding, bundle, out restored, out exists, out failure));
        }

        [TestMethod]
        public void WrongBundleFailsClosedAndStageOneTwoContractsRemainUnchanged()
        {
            MissionAcgBindingRecord record = this.CreateRecord(MissionRollType.FindItem, 40, 500, this.FirstPf());
            MissionAcgLayoutBundle wrong =
                this.catalog.SelectableLayouts.First(x => x.LayoutId != record.Binding.SelectedBundleId);
            MissionAcgMaterializedInstance instance;
            string failure;
            Assert.IsFalse(
                MissionAcgRuntimeMaterializer.TryMaterialize(
                    record,
                    wrong,
                    null,
                    DateTime.UtcNow,
                    out instance,
                    out failure));
            Assert.AreEqual(5, this.catalog.SelectableLayouts.Count);
            Assert.AreEqual(2, MissionAcgInstanceBinding.CurrentFormatVersion);
            Assert.IsNull(
                this.catalog.FindBySourcePlayfield2(
                    MissionAcgLayoutCatalogLoader.ExplicitlyIncompleteShapePlayfield2));
            foreach (MissionAcgLayoutBundle bundle in this.catalog.SelectableLayouts)
            {
                Assert.AreEqual(
                    bundle.ExpectedGeneratorPayloadSha256.ToLowerInvariant(),
                    bundle.GeneratorPayloadSha256.ToLowerInvariant());
            }
        }

        private MissionAcgBindingRecord CreateRecord(MissionRollType type, int salt, int owner, int livePf)
        {
            var ownerIdentity = new MissionAcgIdentityRecord(0xC350, owner);
            MissionAcgLayoutBundle bundle =
                MissionAcgLayoutSelector.Select(
                    this.catalog,
                    new MissionAcgSelectionInput(9000 + salt, type, 42, ownerIdentity));
            DateTime accepted =
                new DateTime(2026, 7, 28, 18, 0, 0, DateTimeKind.Utc).AddMinutes(salt);
            var binding =
                MissionAcgInstanceBinding.CreateDurable(
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.AcceptedQuestIdentityType,
                        0x50020000 + salt),
                    new MissionAcgIdentityRecord(0xDAC3, 0x01020000 + salt),
                    ownerIdentity,
                    null,
                    type,
                    42,
                    9000 + salt,
                    new MissionAcgIdentityRecord(
                        MissionAcgAllocationService.MissionKeyIdentityType,
                        0x60020000 + salt),
                    new MissionAcgIdentityRecord(0x9C50, 710),
                    43308,
                    27595,
                    229.605F + salt,
                    6.504F,
                    452.042F,
                    new MissionAcgIdentityRecord(0xDAC1, 0x3000 + salt),
                    bundle,
                    livePf,
                    accepted,
                    accepted.AddHours(48));
            return new MissionAcgBindingRecord(
                binding,
                new MissionAcgInstanceState(
                    MissionAcgLifecycleState.Accepted,
                    MissionAcgCleanupState.None,
                    accepted,
                    null),
                string.Empty);
        }

        private static MissionAcgMaterializedInstance Materialize(
            MissionAcgBindingRecord record,
            MissionAcgLayoutBundle bundle,
            MissionAcgRuntimeState state)
        {
            MissionAcgMaterializedInstance instance;
            string failure;
            Assert.IsTrue(
                MissionAcgRuntimeMaterializer.TryMaterialize(
                    record,
                    bundle,
                    state,
                    DateTime.UtcNow,
                    out instance,
                    out failure),
                failure);
            return instance;
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

        private int NextPf(int current)
        {
            int value = current + 1;
            while (value == MissionAcgAllocationService.LegacySharedPlayfield2
                   || this.catalog.FindBySourcePlayfield2(value) != null)
            {
                value++;
            }

            return value;
        }

        private static bool Contains(byte[] bytes, int value)
        {
            byte b0 = (byte)(value >> 24);
            byte b1 = (byte)(value >> 16);
            byte b2 = (byte)(value >> 8);
            byte b3 = (byte)value;
            for (int i = 0; i + 4 <= bytes.Length; i++)
            {
                if (bytes[i] == b0 && bytes[i + 1] == b1 && bytes[i + 2] == b2 && bytes[i + 3] == b3)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
