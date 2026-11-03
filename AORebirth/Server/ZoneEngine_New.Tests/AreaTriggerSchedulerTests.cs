namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Enums;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.WorldSimulation;

    [TestClass]
    public sealed class AreaTriggerSchedulerTests
    {
        static ItemSpell Trigger() => new()
        {
            FunctionType = (int)FunctionType.AreaTrigger, Target = 1, TickCount = 1,
            Arguments = [0, 1]
        };

        static ItemSpell Text(int count = 1, int interval = 100) => new()
        {
            FunctionType = (int)FunctionType.SystemText, Target = 2, TickCount = count,
            TickInterval = unchecked((uint)interval), Arguments = ["scheduled action"]
        };

        static ItemSpell Toggle(int count, int interval) => new()
        {
            FunctionType = (int)FunctionType.ToggleFlag, Target = 1, TickCount = count,
            TickInterval = unchecked((uint)interval), Arguments = [(int)CharacterStat.Flags, 12]
        };

        static void Seconds(TriggerTestWorld world, int count)
        {
            for (int i = 0; i < count; i++)
            {
                world.Tick(1);
            }
        }

        [DataTestMethod]
        [DataRow(0, 0)]
        [DataRow(-1, 0)]
        [DataRow(-2, 3)]
        [DataRow(2, 2)]
        public void AuthoredCountsKeepNativeZeroMinusOneAndNegativeSemantics(int count, int expected)
        {
            using var world = new TriggerTestWorld();
            StaticDynel control = world.Add(10, 0, 0, 0, Text(count), Trigger());
            Assert.IsTrue(world.Triggers.BeginUse(control, world.First));
            Seconds(world, 5); // odd pulses at1,3,5; there is no wall-clock catch-up loop.
            Assert.AreEqual(expected, world.FirstSession.Sent.OfType<FormatFeedbackMessage>().Count());
            Assert.AreEqual(0, world.SecondSession.Sent.OfType<FormatFeedbackMessage>().Count());
        }

        [TestMethod]
        public void SignedNegativeIntervalUsesItsMagnitude()
        {
            using var world = new TriggerTestWorld();
            StaticDynel control = world.Add(10, 0, 0, 0, Text(interval: -100), Trigger());
            Assert.IsTrue(world.Triggers.BeginUse(control, world.First));
            Assert.AreEqual(0, world.FirstSession.Sent.OfType<FormatFeedbackMessage>().Count());
            world.Tick(1);
            Assert.AreEqual(1, world.FirstSession.Sent.OfType<FormatFeedbackMessage>().Count());
            Seconds(world, 4);
            Assert.AreEqual(1, world.FirstSession.Sent.OfType<FormatFeedbackMessage>().Count());
        }

        [TestMethod]
        public void RepeatedActivationsKeepIndependentRetainedClones()
        {
            using var world = new TriggerTestWorld();
            StaticDynel control = world.Add(10, 0, 0, 0, Text(), Trigger());
            Assert.IsTrue(world.Triggers.BeginUse(control, world.First));
            Assert.IsTrue(world.Triggers.BeginUse(control, world.First));
            world.Tick(1);
            Assert.AreEqual(2, world.FirstSession.Sent.OfType<FormatFeedbackMessage>().Count());
            Seconds(world, 4);
            Assert.AreEqual(2, world.FirstSession.Sent.OfType<FormatFeedbackMessage>().Count());
        }

        [TestMethod]
        public void RetainedStateEntryActionsWaitForTheOwnersLaterOddPulse()
        {
            var states = new Dictionary<int, StaticObjectState>
            {
                [1] = new([Toggle(2, 150)], [], [])
            };
            using var world = new TriggerTestWorld(new StaticObjectStateMachineCatalog([
                new(71, 1, states, "Explicit test initial-state compatibility")], []));
            StaticDynel machine = world.Add(10, 0, 0, 0);
            machine.Stats.Set(CharacterStat.StateMachine, 71);
            world.Triggers.Initialize();
            // The odd pump occurs before the entry creates this retained row. It cannot execute
            // during the already completed pump; the next odd pulse meets its1.5 second interval.
            world.Tick(1);
            Assert.AreEqual(0, machine.Stats.GetOrZero(CharacterStat.Flags));
            Seconds(world, 2);
            Assert.AreEqual(1 << 12, machine.Stats.GetOrZero(CharacterStat.Flags));
            Seconds(world, 2);
            Assert.AreEqual(0, machine.Stats.GetOrZero(CharacterStat.Flags));
            Seconds(world, 4);
            Assert.AreEqual(0, machine.Stats.GetOrZero(CharacterStat.Flags));
        }

        [TestMethod]
        public void FailedDueCriteriaConsumeCountWithoutResettingLastExecutionTime()
        {
            using var world = new TriggerTestWorld();
            ItemSpell row = Toggle(3, 300);
            row.Requirements.Add(new ItemRequirement
            {
                Operator = (int)Operator.EqualTo, StatNumber = (int)CharacterStat.Level, Value = 999
            });
            StaticDynel control = world.Add(10, 0, 0, 0, row, Trigger());
            Assert.IsTrue(world.Triggers.BeginUse(control, world.First));
            Seconds(world, 3); // first due pump fails its requirement and spends one count.
            Assert.AreEqual(0, control.Stats.GetOrZero(CharacterStat.Flags));
            control.Stats.Set(CharacterStat.Level, 999);
            Seconds(world, 2); // lastTime is still0, so5 seconds is due; resetting it to3 would fail.
            Assert.AreEqual(1 << 12, control.Stats.GetOrZero(CharacterStat.Flags));
            Seconds(world, 4);
            Assert.AreEqual(0, control.Stats.GetOrZero(CharacterStat.Flags));
            Seconds(world, 6);
            Assert.AreEqual(0, control.Stats.GetOrZero(CharacterStat.Flags), "Only two successful counts remained.");
        }

        [TestMethod]
        public void SignalCountdownCountsOddPulsesAndSurvivesStateExit()
        {
            var states = new Dictionary<int, StaticObjectState>
            {
                [1] = new([], [new(136, 3, [])], [new(2, [])]),
                [2] = new([], [], [])
            };
            var catalog = new StaticObjectStateMachineCatalog([
                new(71, 1, states, "Explicit test initial-state compatibility")], [
                new(136, [StaticObjectDoorOperation.Unlock, StaticObjectDoorOperation.Open], null,
                    "Explicit test signal compatibility")]);
            using var world = new TriggerTestWorld(catalog);
            StaticDynel door = world.AddDoor(10, 0, 0, 0);
            door.Stats.Set(CharacterStat.StateMachine, 71);
            world.Triggers.Initialize();
            world.Tick(0); // entry timer is retained even after the unconditional transition.
            Assert.AreEqual(2, door.Stats.GetOrZero(CharacterStat.State));
            Seconds(world, 4);
            Assert.AreEqual(Door.LockedFlag, door.Stats.GetOrZero(CharacterStat.Flags));
            world.Tick(1); // third odd pulse queues before this Run drains the signal queue.
            Assert.AreEqual(Door.OpenFlag, door.Stats.GetOrZero(CharacterStat.Flags));
        }

        [TestMethod]
        public void DelayedPlayerFeedbackCannotFollowAReplacementSession()
        {
            using var world = new TriggerTestWorld();
            StaticDynel control = world.Add(10, 0, 0, 0, Text(), Trigger());
            RecordingZoneSession original = world.FirstSession;
            Assert.IsTrue(world.Triggers.BeginUse(control, world.First));
            var replacement = new RecordingZoneSession { State = SessionState.InPlay };
            replacement.BindPlayer(world.First);
            world.First.Session = replacement;
            Seconds(world, 5);
            Assert.AreEqual(0, original.Sent.OfType<FormatFeedbackMessage>().Count());
            Assert.AreEqual(0, replacement.Sent.OfType<FormatFeedbackMessage>().Count());
        }
    }
}
