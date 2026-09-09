namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    [TestClass]
    public sealed class PlayerDeathRespawnTests
    {
        [TestMethod]
        public void DeathDoesNotRespawnBeforeThreeSecondGrace()
        {
            Player player = CreateWoundedPlayer();

            player.OnDeath();
            player.RequestRespawn();
            player.Tick(2.9);

            Assert.IsTrue(player.IsDead);
        }

        [TestMethod]
        public void DeathRespawnsAfterThreeSecondTick()
        {
            Player player = CreateWoundedPlayer();

            player.OnDeath();
            player.Tick(3.0);

            Assert.IsFalse(player.IsDead);
            Assert.AreEqual(100, player.Stats.GetOrZero(CharacterStat.Health));
        }

        static Player CreateWoundedPlayer()
        {
            Player player = TestWorld.CreatePlayer(1);
            player.Stats.Set(CharacterStat.MaxHealth, 100, StatDetail.Base, dirty: false);
            player.Stats.Set(CharacterStat.Health, 1, StatDetail.Base, dirty: false);
            return player;
        }
    }
}
