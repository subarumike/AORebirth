namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class NpcBrainTests
    {
        [TestMethod]
        public void ProximityAggroAddsOneHateWhenBreedHostilityIsPositive()
        {
            NpcCharacter npc = CreateNpc();
            npc.Stats.Set(NpcAiRules.BreedHostilityStat, 1);
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);

            Player player = TestWorld.CreatePlayer(11);
            player.Position = new Vector3(5, 0, 0);

            brain.TryProximityAggro(player);

            Assert.AreEqual(NpcAiRules.ProximityHate, brain.Hate.ThreatOf(player.Identity));
        }

        [TestMethod]
        public void ProximityAggroDoesNotStackOrFireWhenStatIsZeroOrPlayerIsFar()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            Player near = TestWorld.CreatePlayer(12);
            near.Position = new Vector3(4, 0, 0);
            Player far = TestWorld.CreatePlayer(13);
            far.Position = new Vector3(NpcAiRules.NearbyRange + 5f, 0, 0);

            brain.TryProximityAggro(near);
            Assert.IsTrue(brain.Hate.IsEmpty);

            npc.Stats.Set(NpcAiRules.BreedHostilityStat, 2);
            brain.TryProximityAggro(far);
            Assert.IsTrue(brain.Hate.IsEmpty);

            brain.TryProximityAggro(near);
            brain.TryProximityAggro(near);
            Assert.AreEqual(NpcAiRules.ProximityHate, brain.Hate.ThreatOf(near.Identity));
        }

        [TestMethod]
        public void DamageThreatLetsHigherHateStealTheTarget()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);

            Player first = TestWorld.CreatePlayer(21);
            first.Position = new Vector3(2, 0, 0);
            Player second = TestWorld.CreatePlayer(22);
            second.Position = new Vector3(3, 0, 0);

            brain.AddThreat(first.Identity, 5f);
            brain.AddThreat(second.Identity, 20f);

            Assert.IsTrue(NpcAiRules.TryHighestNearby(
                brain.Hate,
                id => id == first.Identity || id == second.Identity,
                out Identity top,
                out float threat));
            Assert.AreEqual(second.Identity, top);
            Assert.AreEqual(20f, threat);
        }

        [TestMethod]
        public void HoldGroundExtrasReplaceDefaultFightLeaves()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position, NpcAiProfiles.HoldGround);

            Assert.AreSame(HoldGroundCombatExtras.Instance, NpcAiProfiles.HoldGround.CombatExtras);
            Assert.AreNotSame(NpcAiProfiles.Default.CombatExtras, NpcAiProfiles.HoldGround.CombatExtras);
            Assert.IsFalse(brain.HasNearbyHate());
        }

        [TestMethod]
        [Timeout(5000)]
        public void LeashResetFinishesTheTickInsteadOfSpinning()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);

            // Hate with nobody nearby leashes, and the NPC is already home, so return-to-spawn and
            // reset both run inside one tick.
            brain.AddThreat(new Identity { Type = IdentityType.CanbeAffected, Instance = 4242 }, 10f);
            Assert.IsTrue(brain.ShouldLeash());

            brain.Tick(0.1);

            Assert.IsTrue(brain.Hate.IsEmpty);
            Assert.IsFalse(brain.IsBusy);
        }

        [TestMethod]
        [Timeout(5000)]
        public void ResetWithoutHomeHealsInPlace()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(20, 0, 0);
            npc.Stats.Set(CharacterStat.MaxHealth, 100);
            npc.Stats.Set(CharacterStat.Health, 20);
            NpcBrain brain = NpcBrain.Create(npc);

            Assert.IsFalse(brain.HasHome);
            Assert.IsTrue(brain.HasArrivedHome());
            brain.AddThreat(new Identity { Type = IdentityType.CanbeAffected, Instance = 4243 }, 10f);
            Assert.IsTrue(brain.ShouldLeash());

            brain.Tick(0.1);

            Assert.IsTrue(brain.Hate.IsEmpty);
            Assert.AreEqual(100, npc.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(20, npc.Position.x);
        }

        [TestMethod]
        [Timeout(5000)]
        public void ResetWithHomeWalksThenHeals()
        {
            NpcCharacter npc = CreateNpc();
            Vector3 home = new(0, 0, 0);
            npc.Position = new Vector3(10, 0, 0);
            npc.Stats.Set(CharacterStat.MaxHealth, 100);
            npc.Stats.Set(CharacterStat.Health, 20);
            NpcBrain brain = NpcBrain.Create(npc, home);

            brain.AddThreat(new Identity { Type = IdentityType.CanbeAffected, Instance = 4244 }, 10f);
            Assert.IsTrue(brain.ShouldLeash());

            brain.Tick(0.1);
            Assert.IsFalse(brain.Hate.IsEmpty);
            Assert.IsTrue(npc.Motor.HasPath);
            Assert.AreEqual(20, npc.Stats.GetOrZero(CharacterStat.Health));

            npc.Position = home;
            brain.Tick(0.1);

            Assert.IsTrue(brain.Hate.IsEmpty);
            Assert.AreEqual(100, npc.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void HasChanceWithoutPathfinderStaysTrue()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            Player player = TestWorld.CreatePlayer(31);
            player.Position = new Vector3(8, 0, 0);

            Assert.IsTrue(brain.HasChance(player));
            Assert.IsTrue(brain.CanPathTo(player));
        }

        [TestMethod]
        public void HasChanceWithoutLineOfSightUsesPath()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            Player player = TestWorld.CreatePlayer(32);
            player.Position = new Vector3(1, 0, 0);

            Assert.IsTrue(brain.IsInAttackRange(player));
            Assert.IsFalse(npc.HasLineOfSightTo(player));
            Assert.IsFalse(brain.CanAttackNow(player));
            Assert.IsTrue(brain.CanPathTo(player));
            Assert.IsTrue(brain.HasChance(player));
        }

        [TestMethod]
        public void HasChanceWithGraceStaysTrueAfterChance()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            Player player = TestWorld.CreatePlayer(35);
            player.Position = new Vector3(8, 0, 0);

            Assert.IsTrue(brain.HasChance(player));
            Assert.IsTrue(brain.HasChanceWithGrace(player));
        }

        [TestMethod]
        public void CanAttackNowRequiresLineOfSight()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            Player player = TestWorld.CreatePlayer(33);
            player.Position = new Vector3(1, 0, 0);

            Assert.IsTrue(brain.IsInAttackRange(player));
            Assert.IsFalse(npc.HasLineOfSightTo(player));
            Assert.IsFalse(brain.CanAttackNow(player));
        }

        [TestMethod]
        public void HasUnfinishedPathIsTrueWhileLastWaypointIsAway()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            npc.Motor.SetPath(new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) });
            Player player = TestWorld.CreatePlayer(34);
            player.Position = new Vector3(10, 0, 0);

            Assert.IsTrue(npc.Motor.HasPath);
            Assert.IsTrue(brain.HasUnfinishedPath());
            Assert.IsFalse(brain.CanAttackNow(player));
            Assert.IsTrue(brain.HasChance(player));
        }

        [TestMethod]
        public void HasUnfinishedPathIsFalseAtFinalPoint()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(10, 0, 0);
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            npc.Motor.SetPath(new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) });

            Assert.IsFalse(brain.HasUnfinishedPath());
        }

        [TestMethod]
        public void PathEndsUnderNpcWhenLastWaypointIsAtFeet()
        {
            var path = new System.Collections.Generic.List<System.Numerics.Vector3>
            {
                new(0.2f, 0.4f, -0.1f)
            };

            Assert.IsTrue(NpcBrain.PathEndsUnderNpc(new System.Numerics.Vector3(0f, 0f, 0f), path));
        }

        [TestMethod]
        public void PathDoesNotEndUnderNpcWhenLastWaypointTravelsAway()
        {
            var path = new System.Collections.Generic.List<System.Numerics.Vector3>
            {
                new(4f, 0f, 0f),
                new(8f, 0f, 2f)
            };

            Assert.IsFalse(NpcBrain.PathEndsUnderNpc(new System.Numerics.Vector3(0f, 0f, 0f), path));
        }

        static NpcCharacter CreateNpc()
        {
            return new NpcCharacter(
                new Identity { Type = IdentityType.CanbeAffected, Instance = 1001 },
                new StubItemBuilder());
        }
    }
}
