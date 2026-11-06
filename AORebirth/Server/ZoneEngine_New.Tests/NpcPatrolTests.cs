namespace ZoneEngine_New.Tests
{
    using System;
    using System.Reflection;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Movement;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class NpcPatrolTests
    {
        static readonly Vector3[] Route = [new Vector3(10, 0, 0), new Vector3(10, 0, 10)];

        [TestMethod]
        [Timeout(5000)]
        public void HateDuringAPatrolStillLeashes()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = Patroller(npc);

            brain.Tick(1);
            Assert.IsNotNull(brain.PatrolAnchor);

            // Hate with nobody nearby leashes: the root must get past the patrol it was on last think.
            brain.AddThreat(new Identity { Type = IdentityType.CanbeAffected, Instance = 4250 }, 10f);
            brain.Tick(1);

            Assert.IsTrue(brain.Hate.IsEmpty);
        }

        [TestMethod]
        [Timeout(5000)]
        public void APatrolWalksUnlessItIsMarkedRun()
        {
            NpcCharacter walker = CreateNpc();
            walker.Position = new Vector3(0, 0, 0);
            Patroller(walker).Tick(1);
            Assert.AreEqual(MovementState.Walk, walker.Motor.State);

            NpcCharacter runner = CreateNpc(1102);
            runner.Position = new Vector3(0, 0, 0);
            Patroller(runner, run: true).Tick(1);
            Assert.AreNotEqual(MovementState.Walk, runner.Motor.State);
        }

        [TestMethod]
        [Timeout(5000)]
        public void AChatHoldsThePatrolUntilItClosesAndTheLingerEnds()
        {
            NpcCharacter npc = CreateNpc();
            NpcBrain brain = Patroller(npc);
            // Standing on the first waypoint: a patrol think moves the cursor on to the second.
            npc.Position = NpcSpread.Destination(npc, Route[0]);
            Player player = TestWorld.CreatePlayer(21);

            brain.BeginConversation(player);
            brain.Tick(1);
            Assert.IsTrue(brain.IsHeldByConversation);
            Assert.AreEqual(0, brain.PatrolIndex);

            brain.EndConversation(player);
            brain.Tick(1);
            Assert.IsTrue(brain.IsHeldByConversation, "lingers after the chat closes");
            Assert.AreEqual(0, brain.PatrolIndex);

            EndLinger(brain);
            brain.Tick(1);
            Assert.IsFalse(brain.IsHeldByConversation);
            Assert.AreEqual(1, brain.PatrolIndex);
        }

        [TestMethod]
        public void TheHoldLastsUntilTheLastTalkerLeaves()
        {
            NpcBrain brain = Patroller(CreateNpc());
            Player first = TestWorld.CreatePlayer(22);
            Player second = TestWorld.CreatePlayer(23);

            brain.BeginConversation(first);
            brain.BeginConversation(second);
            brain.EndConversation(first);
            EndLinger(brain);

            Assert.IsTrue(brain.IsHeldByConversation);

            brain.EndConversation(second);
            EndLinger(brain);
            Assert.IsFalse(brain.IsHeldByConversation);
        }

        [TestMethod]
        [Timeout(5000)]
        public void HateDuringAChatStillLeashes()
        {
            NpcCharacter npc = CreateNpc();
            npc.Position = new Vector3(0, 0, 0);
            NpcBrain brain = Patroller(npc);
            brain.BeginConversation(TestWorld.CreatePlayer(24));

            brain.AddThreat(new Identity { Type = IdentityType.CanbeAffected, Instance = 4251 }, 10f);
            brain.Tick(1);

            Assert.IsTrue(brain.Hate.IsEmpty);
        }

        static void EndLinger(NpcBrain brain)
            => typeof(NpcBrain).GetField("_conversationHoldUntilUtc", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(brain, DateTime.MinValue);

        static NpcBrain Patroller(NpcCharacter npc, bool run = false)
        {
            NpcBrain brain = NpcBrain.Create(npc, npc.Position);
            brain.PatrolWaypoints = Route;
            brain.PatrolRun = run;
            brain.PatrolEnabled = true;
            return brain;
        }

        static NpcCharacter CreateNpc(int instance = 1101)
            => new(new Identity { Type = IdentityType.CanbeAffected, Instance = instance }, new StubItemBuilder());
    }
}
