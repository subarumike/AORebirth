namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class CharacterMotorPathTests
    {
        [TestMethod]
        public void NavigateTo_WithoutPathfinder_SetsSingleWaypoint()
        {
            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 2101 },
                new StubItemBuilder());
            npc.Position = new Vector3(1, 2, 3);

            npc.Motor.NavigateTo(new Vector3(10, 0, 20));

            Assert.IsTrue(npc.Motor.HasPath);
            var remaining = npc.Motor.CopyRemainingWaypoints();
            Assert.AreEqual(1, remaining.Length);
            Assert.AreEqual(10f, remaining[0].X);
            Assert.AreEqual(0f, remaining[0].Y);
            Assert.AreEqual(20f, remaining[0].Z);
        }
    }
}
