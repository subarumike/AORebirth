namespace ZoneEngine_New.Tests
{
    using System;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility.Config;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    using ZoneEngine_New.Core.DebugMcp;
    using ZoneEngine_New.Core.Entities;

    [TestClass]
    public sealed class DebugMcpTests
    {
        [TestMethod]
        public void DisabledOrOmittedConfigDoesNotStartTheHost()
        {
            Assert.IsNull(DebugMcpHost.Start(null!, null!, null));
            Assert.IsNull(DebugMcpHost.Start(null!, null!, new DebugMcpSettings()));
            Assert.IsNull(DebugMcpHost.Start(null!, null!, new DebugMcpSettings { Enabled = false, ListenIP = "0.0.0.0" }));
        }

        [TestMethod]
        public void NonLoopbackListenAddressIsRejected()
        {
            var settings = new DebugMcpSettings { Enabled = true, ListenIP = "0.0.0.0", Port = 7590 };
            StartupValidationException exception = Assert.ThrowsExactly<StartupValidationException>(
                () => DebugMcpHost.Start(null!, null!, settings));
            StringAssert.Contains(exception.Message, "loopback");
        }

        [TestMethod]
        public void EnabledDefaultsBindLoopback()
        {
            DebugMcpEndpoint? endpoint = DebugMcpOptions.Resolve(new DebugMcpSettings { Enabled = true });
            Assert.IsNotNull(endpoint);
            Assert.AreEqual("127.0.0.1", endpoint.ListenIP);
            Assert.AreEqual(7590, endpoint.Port);
            Assert.AreEqual("http://127.0.0.1:7590/mcp", endpoint.Url);
        }

        [TestMethod]
        public void SnapshotFormatsPlayerAndDynel()
        {
            var player = new Player(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 7 },
                new StubLogger(),
                new StubItemBuilder())
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                Position = new Vector3(10, 20, 30)
            };
            player.Stats.Set(CharacterStat.Level, 5);
            player.Stats.Set(CharacterStat.Health, 100);
            player.Stats.Set(CharacterStat.MaxHealth, 200);
            player.Stats.Set(CharacterStat.CurrentNano, 50);
            player.Stats.Set(CharacterStat.MaxNanoEnergy, 80);
            player.Stats.Set(CharacterStat.Profession, (int)Profession.Soldier);
            player.Stats.Set(CharacterStat.Breed, (int)Breed.Solitus);

            PlayerView view = ZoneDebugSnapshots.ProjectPlayer(player);
            Assert.AreEqual(7, view.CharacterId);
            Assert.AreEqual("Ada Lovelace", view.Name);
            Assert.AreEqual(10, view.X);
            Assert.AreEqual(20, view.Y);
            Assert.AreEqual(30, view.Z);
            Assert.AreEqual(5, view.Level);
            Assert.AreEqual(100, view.Health);
            Assert.AreEqual(200, view.MaxHealth);
            Assert.AreEqual(50, view.Nano);
            Assert.AreEqual(80, view.MaxNano);
            Assert.AreEqual("Soldier", view.Profession);
            Assert.AreEqual("Solitus", view.Breed);
            Assert.IsFalse(double.IsNaN(view.HeadingDegrees));

            var dynel = new Dynel(new Identity { Type = IdentityType.Terminal, Instance = 42 })
            {
                Position = new Vector3(1, 2, 3)
            };
            DynelView projected = ZoneDebugSnapshots.ProjectDynel(dynel);
            Assert.AreEqual("Dynel", projected.Name);
            Assert.AreEqual("Terminal", projected.IdentityType);
            Assert.AreEqual(42, projected.Instance);
            Assert.AreEqual(1, projected.X);
            Assert.AreEqual(2, projected.Y);
            Assert.AreEqual(3, projected.Z);
        }
    }
}
