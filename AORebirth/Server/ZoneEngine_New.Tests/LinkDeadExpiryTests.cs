namespace ZoneEngine_New.Tests
{
    using System;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class LinkDeadExpiryTests
    {
        [TestMethod]
        public void HasLinkDeadExpiredIsFalseUntilTheDeadline()
        {
            var player = TestWorld.CreatePlayer(18);
            DateTime now = DateTime.UtcNow;
            player.EnterLinkDead(TimeSpan.FromSeconds(60));

            Assert.IsFalse(player.HasLinkDeadExpired(now));
            Assert.IsFalse(player.HasLinkDeadExpired(now.AddSeconds(59)));
            Assert.IsTrue(player.HasLinkDeadExpired(player.LinkDeadUntilUtc!.Value));
            Assert.IsTrue(player.HasLinkDeadExpired(now.AddMinutes(2)));
        }

        [TestMethod]
        public void ZeroTimeoutExpiresImmediately()
        {
            var player = TestWorld.CreatePlayer(18);
            player.EnterLinkDead(TimeSpan.Zero);

            Assert.IsTrue(player.HasLinkDeadExpired(DateTime.UtcNow));
        }
    }
}