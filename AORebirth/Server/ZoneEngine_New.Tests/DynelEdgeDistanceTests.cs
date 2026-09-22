namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class DynelEdgeDistanceTests
    {
        [TestMethod]
        public void Scale100CharactersSubtractBothRadii()
        {
            Player a = CreateScaledPlayer(1, 100);
            Player b = CreateScaledPlayer(2, 100);
            a.Position = new Vector3(0, 0, 0);
            b.Position = new Vector3(10, 0, 0);

            Assert.AreEqual(9.0, a.GetEdgeDistanceTo(b), 1e-6);
        }

        [TestMethod]
        public void LargerScaleIncreasesRadius()
        {
            Player large = CreateScaledPlayer(1, 200);
            Player normal = CreateScaledPlayer(2, 100);
            large.Position = new Vector3(0, 0, 0);
            normal.Position = new Vector3(10, 0, 0);

            Assert.AreEqual(8.5, large.GetEdgeDistanceTo(normal), 1e-6);
        }

        [TestMethod]
        public void StaticDynelHasNoRadius()
        {
            Player player = CreateScaledPlayer(1, 100);
            var prop = new Dynel(new Identity { Type = IdentityType.CanbeAffected, Instance = 9 });
            player.Position = new Vector3(0, 0, 0);
            prop.Position = new Vector3(10, 0, 0);

            Assert.AreEqual(9.5, player.GetEdgeDistanceTo(prop), 1e-6);
        }

        [TestMethod]
        public void UnsetScaleTreatsAs100()
        {
            Player player = TestWorld.CreatePlayer(1);
            Player other = CreateScaledPlayer(2, 100);
            player.Position = new Vector3(0, 0, 0);
            other.Position = new Vector3(10, 0, 0);

            Assert.AreEqual(9.0, player.GetEdgeDistanceTo(other), 1e-6);
        }

        [TestMethod]
        public void OverlapIsNegative()
        {
            Player a = CreateScaledPlayer(1, 100);
            Player b = CreateScaledPlayer(2, 100);
            a.Position = new Vector3(0, 0, 0);
            b.Position = new Vector3(0.4, 0, 0);

            Assert.AreEqual(-0.6, a.GetEdgeDistanceTo(b), 1e-6);
        }

        static Player CreateScaledPlayer(int instance, int scale)
        {
            Player player = TestWorld.CreatePlayer(instance);
            player.Stats.Set(CharacterStat.Scale, scale);
            return player;
        }
    }
}
