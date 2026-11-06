namespace ZoneEngine_New.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using N3Lite;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using ZoneEngine_New.Core.Entities;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class DoorStateTests
    {
        // Finalized Broken Shores PF665 capture, 2026-11-03 22:50:06Z: all four observed subclass bodies.
        // These establish the transport fields, not the Crypt of Home's authored trigger behavior.
        [DataTestMethod]
        [DataRow(false, false, false, "0000000200000000000000000003F1")]
        [DataRow(true, false, false, "0000000200010000000000000003F1")]
        [DataRow(false, true, true, "0000000201000000000001000003F1")]
        [DataRow(true, false, true, "0000000200010000000001000003F1")]
        public void DoorStatusSerializesCapturedStateBodies(bool open, bool locked, bool closeInhibit, string expected)
        {
            var identity = new Identity { Type = IdentityType.Door, Instance = 281279549 };
            var body = Door.StatusMessage(identity, open, locked, closeInhibit);
            using var stream = new MemoryStream();
            new MessageSerializer().Serialize(stream, new Message
            {
                Header = new Header { MessageId = 0xdfdf, PacketType = body.PacketType, Unknown = 1, Sender = 3508, Receiver = 1 },
                Body = body
            });

            byte[] packet = stream.ToArray();
            CollectionAssert.AreEqual(Convert.FromHexString("4C7D403B0000C74810C3FC3D00" + expected), packet.AsSpan(16).ToArray());
        }

        [TestMethod]
        public void OrdinaryTouchUsesTheSameFlagsAndPreservesUnrelatedBits()
        {
            const int unrelated = 0x10001;
            var door = new Door(new Identity { Type = IdentityType.Door, Instance = 1 }, 2,
                unrelated | Door.LockedFlag, 0);
            Assert.IsFalse(door.OpenByTouch());
            Assert.IsFalse(door.CloseInhibited);

            door.ApplyFlags(unrelated);
            Assert.IsTrue(door.OpenByTouch());
            Assert.AreEqual(unrelated | Door.OpenFlag, door.Stats.GetOrZero(CharacterStat.Flags));
            Assert.IsTrue(door.CloseAfterTouch());
            Assert.AreEqual(unrelated, door.Flags);
            Assert.IsFalse(door.CloseInhibited);
        }

        [TestMethod]
        public void AuthoredOpenAndRelockKeepLockAndOpenIndependent()
        {
            var door = new Door(new Identity { Type = IdentityType.Door, Instance = 1 }, 2, Door.LockedFlag, 0);
            door.ApplyFlags(Door.OpenFlag, closeInhibit: true);
            Assert.IsTrue(door.IsOpen);
            Assert.IsFalse(door.IsLocked);
            Assert.IsTrue(door.CloseInhibited);

            door.ApplyFlags(Door.LockedFlag);
            Assert.IsFalse(door.IsOpen);
            Assert.IsTrue(door.IsLocked);
            Assert.IsTrue(door.CloseInhibited, "The capture retains the latch through an explicit locked/closed update.");
            Assert.IsFalse(door.OpenByTouch());
        }

        [TestMethod]
        public void DynamicDoorSpawnCompanionRestoresAuthoredLatchWithoutChangingOrdinarySpawn()
        {
            var door = new Door(new Identity { Type = IdentityType.Door, Instance = 10 }, 2, Door.LockedFlag, 0);
            Assert.AreEqual(0, door.BuildSpawnCompanionMessages().Count());
            door.ApplyFlags(Door.OpenFlag, closeInhibit: true);
            var opened = (DoorStatusUpdateMessage)door.BuildSpawnCompanionMessages().Single();
            Assert.AreEqual(door.Identity, opened.Identity);
            Assert.AreEqual((byte)0, opened.Unknown2);
            Assert.AreEqual((byte)1, opened.Unknown3);
            Assert.AreEqual((byte)1, opened.Unknown5);

            door.ApplyFlags(Door.LockedFlag);
            var relocked = (DoorStatusUpdateMessage)door.BuildSpawnCompanionMessages().Single();
            Assert.AreEqual((byte)1, relocked.Unknown2);
            Assert.AreEqual((byte)0, relocked.Unknown3);
            Assert.AreEqual((byte)1, relocked.Unknown5);
        }

        [TestMethod]
        public void OrdinaryBreakAndEnterStillUnlocksWithoutSettingAuthoredLatch()
        {
            using var f = new TriggerTestWorld();
            f.First.Stats.Set(CharacterStat.Health, 100);
            var door = new Door(new Identity { Type = IdentityType.Door, Instance = 10 }, 2,
                Door.LockedFlag | 0x10000, 0, lockDifficulty: 10) { Playfield = f.Playfield };
            f.Registry.Register(door);
            f.Locality.RegisterDynel(door);
            Assert.IsFalse(door.TryUse(f.First));
            Assert.AreEqual(BreakAndEnterResult.Failed, door.TryBreakAndEnter(f.First, 9));
            Assert.IsTrue(door.IsLocked);

            Assert.AreEqual(BreakAndEnterResult.Unlocked, door.TryBreakAndEnter(f.First, 10));
            Assert.AreEqual(0x10000 | Door.OpenFlag, door.Stats.GetOrZero(CharacterStat.Flags));
            Assert.IsFalse(door.IsLocked);
            Assert.IsFalse(door.CloseInhibited);
            Assert.IsTrue(door.TryUse(f.First));
            Assert.AreEqual(0x10000, door.Flags);
        }

        [TestMethod]
        public void BoundDoorWithoutCollisionPublishesToBothViewersAndReplaysCurrentState()
        {
            using var f = new TriggerTestWorld();
            StaticDynel door = f.AddDoor(10, 10, 0, 0);
            f.FirstSession.Sent.Clear();
            f.SecondSession.Sent.Clear();
            Assert.AreEqual(0, f.World.Doors.Count, "Publication must not require a collision blocker.");

            Assert.IsTrue(f.World.ApplyDoorFlags(f.Playfield, door, Door.OpenFlag, closeInhibit: true));
            foreach (RecordingZoneSession session in new[] { f.FirstSession, f.SecondSession })
            {
                var message = session.Sent.OfType<DoorStatusUpdateMessage>().Single();
                Assert.AreEqual(door.Identity, message.Identity);
                Assert.AreEqual((byte)0, message.Unknown2);
                Assert.AreEqual((byte)1, message.Unknown3);
                Assert.AreEqual((byte)1, message.Unknown5);
            }

            Assert.IsTrue(f.World.ApplyDoorFlags(f.Playfield, door, Door.OpenFlag));
            Assert.IsTrue(f.World.ApplyDoorFlags(f.Playfield, door, Door.OpenFlag | 0x10000));
            Assert.AreEqual(1, f.FirstSession.Sent.OfType<DoorStatusUpdateMessage>().Count());
            Assert.AreEqual(1, f.SecondSession.Sent.OfType<DoorStatusUpdateMessage>().Count());
            Assert.AreEqual(Door.OpenFlag | 0x10000, door.Stats.GetOrZero(CharacterStat.Flags));

            var reconnect = new RecordingZoneSession();
            f.World.Doors.SendStates(reconnect);
            var restored = reconnect.Sent.OfType<DoorStatusUpdateMessage>().Single();
            Assert.AreEqual((byte)1, restored.Unknown3);
            Assert.AreEqual((byte)1, restored.Unknown5);

            Assert.IsTrue(f.World.ApplyDoorFlags(f.Playfield, door, Door.LockedFlag | 0x10000));
            reconnect.Sent.Clear();
            f.World.Doors.SendStates(reconnect);
            restored = reconnect.Sent.OfType<DoorStatusUpdateMessage>().Single();
            Assert.AreEqual((byte)1, restored.Unknown2);
            Assert.AreEqual((byte)0, restored.Unknown3);
            Assert.AreEqual((byte)1, restored.Unknown5, "Relock uses locked/closed state, not an unproven latch-clear packet.");
        }

        [TestMethod]
        public void BoundDoorFlagsDriveExistingLosBlockerAndDoNotDuplicateReplay()
        {
            using var f = new TriggerTestWorld();
            StaticDynel door = f.AddDoor(10, 10, 0, 0);
            f.World.Doors.Add(10, door.Position, 0, 1, 2, 1, flags: 0);
            int changes = 0;
            f.World.Doors.Changed += () => changes++;
            var from = new Vec3(10, 1, -3);
            var to = new Vec3(10, 1, 3);
            Assert.IsTrue(f.World.Doors.Blocks(from, to), "Bound locked flags override the bake's initial flags.");

            Assert.IsTrue(f.World.ApplyDoorFlags(f.Playfield, door, Door.OpenFlag));
            Assert.IsFalse(f.World.Doors.Blocks(from, to));
            Assert.AreEqual(1, changes);
            Assert.IsTrue(f.World.ApplyDoorFlags(f.Playfield, door, Door.LockedFlag));
            Assert.IsTrue(f.World.Doors.Blocks(from, to));
            Assert.AreEqual(2, changes);

            var reconnect = new RecordingZoneSession();
            f.World.Doors.SendStates(reconnect);
            Assert.AreEqual(1, reconnect.Sent.OfType<DoorStatusUpdateMessage>().Count());
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ContactCloseRespectsAuthoredLatchAndExplicitRelock(bool closeInhibit)
        {
            using var f = new TriggerTestWorld();
            StaticDynel door = f.AddDoor(10, 0, 0, 0, locked: false);
            f.World.Doors.Add(10, door.Position, 0, 1, 2, 1, flags: 0);
            f.First.Stats.Set(CharacterStat.Health, 100);
            f.Second.Stats.Set(CharacterStat.Health, 100);
            f.Second.Position = new Vector3(100, 0, 0);
            if (closeInhibit)
                Assert.IsTrue(f.World.ApplyDoorFlags(f.Playfield, door, Door.OpenFlag, closeInhibit: true));

            f.World.Doors.Tick(f.Playfield, 0.1);
            Assert.AreEqual(Door.OpenFlag, door.Stats.GetOrZero(CharacterStat.Flags));
            f.First.Position = new Vector3(100, 0, 0);
            f.World.Doors.Tick(f.Playfield, 0.1);
            Assert.AreEqual(closeInhibit ? Door.OpenFlag : 0, door.Stats.GetOrZero(CharacterStat.Flags));

            Assert.IsTrue(f.World.ApplyDoorFlags(f.Playfield, door, Door.LockedFlag));
            f.First.Position = new Vector3(0, 0, 0);
            f.World.Doors.Tick(f.Playfield, 0.1);
            Assert.AreEqual(Door.LockedFlag, door.Stats.GetOrZero(CharacterStat.Flags), "Contact must not reopen a relocked door.");
        }
    }
}
