namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Enums;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.WorldSimulation;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class AreaTriggerTests
    {
        static StaticObjectStateMachineCatalog PolicyCatalog() => new([], [
            new(136, [StaticObjectDoorOperation.Unlock, StaticObjectDoorOperation.Open], null, "Explicit test compatibility"),
            new(80, [StaticObjectDoorOperation.Close, StaticObjectDoorOperation.Lock], null, "Explicit test compatibility"),
            new(256, [], false, "Explicit test compatibility"),
            new(4, [], true, "Explicit test compatibility")]);

        static ItemSpell Area(int signal, int radius = 1, int target = 1) => new()
        { FunctionType = (int)FunctionType.AreaTrigger, Target = target, TickCount = 1, Arguments = [signal, radius] };

        static ItemSpell Remote(int signal, int x, int y, int z, uint delay = 0) => new()
        { FunctionType = (int)FunctionType.RemoteAreaTrigger, Target = 2, TickCount = 1,
            TickInterval = delay, Arguments = [x, y, z, signal, 1] };

        static AreaTriggerContext Context(TriggerTestWorld f, StaticDynel owner)
            => new(owner, f.First, f.First, f.First, f.First.Session);

        static void Advance(TriggerTestWorld f, double seconds)
        {
            for (int i = 0; i < Math.Ceiling(seconds / 0.05); i++)
                f.Tick(0.05);
        }

        [DataTestMethod]
        [DataRow(0)] [DataRow(-20)] [DataRow(1)]
        public void AreaUsesOwnerCenterStrictXyzAndNonpositiveRadiusOne(int radius)
        {
            using var f = new TriggerTestWorld(PolicyCatalog());
            StaticDynel owner = f.Add(10, 0, 0, 0);
            StaticDynel inside = f.AddDoor(11, 0.5, 0.5, 0.5);
            StaticDynel edge = f.AddDoor(12, 1, 0, 0);
            StaticDynel above = f.AddDoor(13, 0, 2, 0);
            f.First.Position = new Vector3(100, 100, 100);
            Assert.IsTrue(f.Triggers.TryExecute(Area(136, radius), Context(f, owner)));
            f.Tick(0.05);
            Assert.AreEqual(Door.OpenFlag, inside.Stats.GetOrZero(CharacterStat.Flags));
            Assert.AreEqual(Door.LockedFlag, edge.Stats.GetOrZero(CharacterStat.Flags));
            Assert.AreEqual(Door.LockedFlag, above.Stats.GetOrZero(CharacterStat.Flags));
        }

        [TestMethod]
        public void RemoteUsesSignedCoordinatesAndDoesNotPickNearestDoor()
        {
            using var f = new TriggerTestWorld(PolicyCatalog());
            StaticDynel control = f.Add(10, 500, 500, 500);
            StaticDynel chosen = f.AddDoor(11, -20, 15, -30);
            StaticDynel nearbyControl = f.AddDoor(12, 500.1, 500, 500);
            Assert.IsTrue(f.Triggers.TryExecute(Remote(136, -20, 15, -30), Context(f, control)));
            f.Tick(0.05);
            Assert.AreEqual(Door.OpenFlag, chosen.Stats.GetOrZero(CharacterStat.Flags));
            Assert.AreEqual(Door.LockedFlag, nearbyControl.Stats.GetOrZero(CharacterStat.Flags));
        }

        [TestMethod]
        public void AreaDoesNotExcludeItsOwnControlFromSignals()
        {
            using var f = new TriggerTestWorld(PolicyCatalog());
            StaticDynel control = f.Add(10, 0, 0, 0);
            Assert.IsTrue(f.Triggers.TryExecute(Area(256), Context(f, control)));
            f.Tick(0.05);
            Assert.IsFalse(f.Triggers.IsUseEnabled(control));
            Assert.IsTrue(f.Triggers.TryExecute(Area(4), Context(f, control)));
            f.Tick(0.05);
            Assert.IsTrue(f.Triggers.IsUseEnabled(control));
        }

        [TestMethod]
        public void AnotherPlayfieldCannotSupplyTriggerExecutionContext()
        {
            using var first = new TriggerTestWorld(PolicyCatalog());
            using var second = new TriggerTestWorld(PolicyCatalog());
            StaticDynel control = first.Add(10, 0, 0, 0);
            StaticDynel other = second.AddDoor(11, 0, 0, 0);
            Assert.IsFalse(second.Triggers.TryExecute(Area(136), Context(first, control)));
            Assert.AreEqual(Door.LockedFlag, other.Stats.GetOrZero(CharacterStat.Flags));
        }

        [DataTestMethod]
        [DataRow(false, 2)] [DataRow(true, 3)]
        public void SimultaneousSignalsUseDeclaredCoalescingOrServerSerialization(bool serialize, int expected)
        {
            var states = new Dictionary<int, StaticObjectState>
            {
                [1] = new([], [], [new(2, [new(20, 1024, 0)])]),
                [2] = new([], [], [new(3, [new(20, 1024, 0)])]),
                [3] = new([], [], [])
            };
            var catalog = new StaticObjectStateMachineCatalog([
                new(71, 1, states, "Explicit test initial-state compatibility", serialize)], []);
            using var f = new TriggerTestWorld(catalog);
            StaticDynel machine = f.Add(10, 0, 0, 0);
            machine.Stats.Set(CharacterStat.StateMachine, 71);
            f.Triggers.Initialize();
            // No distinct-player restriction: both signals deliberately carry the same invoking session.
            f.Triggers.TryExecute(Area(1024), Context(f, machine));
            f.Triggers.TryExecute(Area(1024), Context(f, machine));
            f.Tick(0.05);
            Assert.AreEqual(2, machine.Stats.GetOrZero(CharacterStat.State));
            f.Tick(0.05);
            Assert.AreEqual(expected, machine.Stats.GetOrZero(CharacterStat.State));
        }

        [TestMethod]
        public void SignalConsumptionSurvivesLaterFailingCompoundCondition()
        {
            var states = new Dictionary<int, StaticObjectState>
            {
                [1] = new([], [], [
                    new(2, [new(20, 1024, 0), new(0, (int)CharacterStat.Level, 999), new(4, 0, 0)]),
                    new(2, [new(20, 1024, 0)])]),
                [2] = new([], [], [])
            };
            using var f = new TriggerTestWorld(new StaticObjectStateMachineCatalog([
                new(71, 1, states, "Explicit test compatibility")], []));
            StaticDynel machine = f.Add(10, 0, 0, 0);
            machine.Stats.Set(CharacterStat.StateMachine, 71);
            f.Triggers.Initialize();
            f.Triggers.TryExecute(Area(1024), Context(f, machine));
            f.Tick(0.05);
            Assert.AreEqual(1, machine.Stats.GetOrZero(CharacterStat.State));
        }

        [TestMethod]
        public void AdmittedRemoteRelockSurvivesInvokerLeavingThePlayfield()
        {
            using var f = new TriggerTestWorld(PolicyCatalog());
            StaticDynel door = f.AddDoor(20, 10, 20, 30);
            StaticDynel control = f.Add(10, 0, 0, 0, Remote(136, 10, 20, 30), Remote(80, 10, 20, 30, 200));
            Assert.IsTrue(f.Triggers.BeginUse(control, f.First));
            f.Tick(0.05);
            Assert.AreEqual(Door.OpenFlag, door.Stats.GetOrZero(CharacterStat.Flags));
            f.FirstSession.Close();
            f.First.Playfield = null;
            Advance(f, 5);
            Assert.AreEqual(Door.LockedFlag, door.Stats.GetOrZero(CharacterStat.Flags));
        }

        [TestMethod]
        public void UnloadCancelsPendingDoorEffects()
        {
            using var f = new TriggerTestWorld(PolicyCatalog());
            StaticDynel door = f.AddDoor(20, 10, 20, 30);
            StaticDynel control = f.Add(10, 0, 0, 0, Remote(136, 10, 20, 30, 200));
            Assert.IsTrue(f.Triggers.BeginUse(control, f.First));
            f.Triggers.Dispose();
            Advance(f, 5);
            Assert.AreEqual(Door.LockedFlag, door.Stats.GetOrZero(CharacterStat.Flags));
            Assert.IsFalse(f.Triggers.BeginUse(control, f.First));
        }

        [TestMethod]
        public void CopyRetainsOriginalActionMetadata()
        {
            ItemSpell original = Remote(80, -1, 2, -3, 150);
            original.Metadata35 = -73;
            ItemSpell copy = original.Copy();
            Assert.AreEqual(original.Metadata35, copy.Metadata35);
            Assert.AreEqual(original.Target, copy.Target);
            Assert.AreEqual(original.TickCount, copy.TickCount);
            Assert.AreEqual(original.TickInterval, copy.TickInterval);
            CollectionAssert.AreEqual(original.Arguments, copy.Arguments);
        }
    }
}
