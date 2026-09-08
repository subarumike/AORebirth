namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Utility.Network;
    using ZoneEngine_New.Core.Data;

    [TestClass]
    public sealed class RuntimeStartupTests
    {
        [TestMethod]
        public void ValidLifecycleArgumentsRetainBothWrapperForms()
        {
            RuntimeStartup.ValidateArguments(["/autostart", "/headless", "/shutdown-file", "shutdown.signal"]);
            RuntimeStartup.ValidateArguments(["--headless", "--shutdown-file", "/tmp/shutdown.signal"]);
        }

        [TestMethod]
        public void UnknownOrAmbiguousArgumentsFailClosed()
        {
            Assert.ThrowsException<ArgumentException>(() => RuntimeStartup.ValidateArguments(["--migrate"]));
            Assert.ThrowsException<ArgumentException>(() => RuntimeStartup.ValidateArguments(["--shutdown-file"]));
            Assert.ThrowsException<ArgumentException>(() => RuntimeStartup.ValidateArguments(["--shutdown-file", "--headless"]));
            Assert.ThrowsException<ArgumentException>(() => RuntimeStartup.ValidateArguments(["/headless", "--headless"]));
            Assert.ThrowsException<ArgumentException>(() => RuntimeStartup.ValidateArguments(["--validate-startup", "--validate-database"]));
        }

        [TestMethod]
        public void BindDefaultsToLoopbackAndNeverGuessesPublicOnInvalidInput()
        {
            Assert.AreEqual("127.0.0.1", EngineBindPolicy.Resolve(null!).AddressText);
            Assert.AreEqual("0.0.0.0", EngineBindPolicy.Resolve("Public").AddressText);
            Assert.ThrowsException<InvalidOperationException>(() => EngineBindPolicy.Resolve("invalid"));
            Assert.ThrowsException<InvalidOperationException>(() => EngineBindPolicy.Resolve(""));
            Assert.ThrowsException<InvalidOperationException>(() => RuntimeStartup.ValidatePort(0));
            Assert.ThrowsException<InvalidOperationException>(() => RuntimeStartup.ValidatePort(65536));
        }

        [TestMethod]
        public void DeploymentDatabaseTargetCannotSilentlySelectAnotherSchema()
        {
            RuntimeStartup.ValidateDatabaseTarget("approved", "approved", true);
            RuntimeStartup.ValidateDatabaseTarget("local", null, false);
            Assert.ThrowsException<StartupValidationException>(() => RuntimeStartup.ValidateDatabaseTarget("wrong", "approved", true));
            Assert.ThrowsException<StartupValidationException>(() => RuntimeStartup.ValidateDatabaseTarget("approved", null, true));
            Assert.ThrowsException<StartupValidationException>(() => RuntimeStartup.ValidateDatabaseTarget("Approved", "approved", false));
        }

        [TestMethod]
        public void ItemAllocatorConstructionIsReadOnlyAndConcurrentIdsAreUnique()
        {
            var repository = new LeaseRepository();
            var allocator = new ItemInstanceIdAllocator(repository, new StubLogger());
            Assert.AreEqual(0, repository.Leases);
            var ids = new System.Collections.Concurrent.ConcurrentBag<int>();
            Parallel.For(0, ItemInstanceIdAllocator.LeaseBlockSize + 1, _ => ids.Add(allocator.Allocate()));
            Assert.AreEqual(2, repository.Leases);
            Assert.AreEqual(ids.Count, new HashSet<int>(ids).Count);
        }

        private sealed class LeaseRepository : IInventoryRepository
        {
            public int Leases;
            public int LeaseInstanceIdBlock(int count) => 1 + Leases++ * count;
            public IReadOnlyList<ItemInstanceRecord> GetCarriedItems(int characterId) => throw new NotSupportedException();
            public IReadOnlyList<ItemInstanceRecord> GetBankItems(int characterId) => throw new NotSupportedException();
            public IReadOnlyList<ItemInstanceRecord> GetContainerItems(int containerInstanceId) => throw new NotSupportedException();
            public ItemInstanceRecord Insert(ItemInstanceRecord item) => throw new NotSupportedException();
            public void UpdateLocation(int instanceId, int containerType, int containerInstance, int placement) => throw new NotSupportedException();
            public void UpdateLocations(IReadOnlyList<ItemLocationUpdate> locations) => throw new NotSupportedException();
            public void PersistNewAndUpdateLocations(IReadOnlyList<ItemInstanceRecord> inserts,
                IReadOnlyList<ItemLocationUpdate> updates) => throw new NotSupportedException();
        }
    }
}
