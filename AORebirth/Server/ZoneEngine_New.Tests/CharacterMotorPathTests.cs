namespace ZoneEngine_New.Tests
{
    using System;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Movement;

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

        [TestMethod]
        public void Tick_OnPath_CoversRunSpeedFromTheStart()
        {
            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 2102 },
                new StubItemBuilder());
            npc.Position = new Vector3(0, 0, 0);
            npc.Motor.NavigateTo(new Vector3(10, 0, 0));

            for (int i = 0; i < 20; i++)
                npc.Motor.Tick(0.05);

            Assert.IsTrue(npc.Motor.HasPath);
            Assert.AreEqual(5f, npc.Motor.Vehicle.MaxVel, 1e-4f, "State 3 with RunSpeed 0 is the 5 m/s base.");
            Assert.AreEqual(5f, (float)npc.Position.x, 0.1f, "Without NPC acceleration the body runs at full speed.");
            Assert.AreEqual(0f, (float)npc.Position.z, 0.01f);
        }

        [TestMethod]
        public void Tick_OnPath_SnapsFacingToSegmentDirection()
        {
            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 2104 },
                new StubItemBuilder());
            npc.Position = new Vector3(0, 0, 0);
            npc.Motor.NavigateTo(new Vector3(10, 0, 0));

            npc.Motor.Tick(0.05);

            float half = MathF.PI * 0.25f;
            Assert.AreEqual(0f, npc.Rotation.xf, 0.01f);
            Assert.AreEqual(MathF.Sin(half), npc.Rotation.yf, 0.01f);
            Assert.AreEqual(0f, npc.Rotation.zf, 0.01f);
            Assert.AreEqual(MathF.Cos(half), npc.Rotation.wf, 0.01f);
        }

        [TestMethod]
        public void Tick_OnPath_ReachesEndAndClears()
        {
            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 2103 },
                new StubItemBuilder());
            npc.Position = new Vector3(0, 0, 0);
            npc.Motor.NavigateTo(new Vector3(10, 0, 0));

            bool completed = false;
            npc.Motor.PathCompleted += () => completed = true;
            for (int i = 0; i < 120 && npc.Motor.HasPath; i++)
                npc.Motor.Tick(0.05);

            Assert.IsTrue(completed);
            Assert.IsFalse(npc.Motor.HasPath);
            Assert.AreEqual(10f, (float)npc.Position.x, 0.3f);
            Assert.AreEqual(0, npc.Motor.CopyRemainingWaypoints().Length);
        }

        [TestMethod]
        public void Tick_NewPath_StartsAtFullSpeedWithoutNpcAcceleration()
        {
            Assert.IsFalse(MovementConfig.NpcAccelerationEnabled);
            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 2105 },
                new StubItemBuilder());
            npc.Position = new Vector3(0, 0, 0);
            npc.Motor.Tick(0.05);
            npc.Motor.NavigateTo(new Vector3(50, 0, 0));

            npc.Motor.Tick(0.05);

            Assert.AreEqual(npc.Motor.Vehicle.MaxVel, npc.Motor.Vehicle.Speed, 0.05f);
        }

        [TestMethod]
        public void ReplacePath_KeepsTheGuideLeadSoTheNpcDoesNotBrake()
        {
            NpcCharacter npc = new(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 2106 },
                new StubItemBuilder());
            npc.Position = new Vector3(0, 0, 0);
            npc.Motor.NavigateTo(new Vector3(50, 0, 0));
            for (int i = 0; i < 20; i++)
                npc.Motor.Tick(0.05);
            float speed = npc.Motor.Vehicle.Speed;
            Assert.AreEqual(npc.Motor.Vehicle.MaxVel, speed, 0.05f);

            npc.Motor.ReplacePath(new[] { new Vector3(npc.Position.x, 0, 0), new Vector3(50, 0, 0) });
            double before = npc.Position.x;
            for (int i = 0; i < 4; i++)
                npc.Motor.Tick(0.05);

            Assert.AreEqual(speed, npc.Motor.Vehicle.Speed, 0.05f);
            Assert.AreEqual(speed * 0.2, npc.Position.x - before, 0.05);
        }
    }
}
