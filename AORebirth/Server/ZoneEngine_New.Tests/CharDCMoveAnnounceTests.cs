namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield.Locality;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    [TestClass]
    public sealed class CharDCMoveAnnounceTests
    {
        [TestMethod]
        public void AcceptedCharDCMoveIsAnnouncedToOtherCellOccupants()
        {
            var locality = new PlayfieldLocality(954, metaData: null);
            Player mover = TestWorld.CreatePlayer(18, "Mover");
            Player observer = TestWorld.CreatePlayer(19, "Observer");
            var moverSession = new RecordingZoneSession { State = SessionState.InPlay };
            var observerSession = new RecordingZoneSession { State = SessionState.InPlay };
            mover.EnterOnline(moverSession);
            observer.EnterOnline(observerSession);
            locality.RegisterDynel(mover);
            locality.RegisterDynel(observer);

            var move = new CharDCMoveMessage
            {
                Unknown = 1,
                MoveType = (byte)MovementAction.ForwardStart,
                Heading = new MsgQuaternion { X = 0, Y = 0, Z = 0, W = 1 },
                Coordinates = new MsgVector3 { X = 10, Y = 5, Z = 12 },
                Unknown1 = 7,
                AuxA = 1.5f,
                AuxB = 2.5f
            };

            new CharDCMoveMessageHandler().Handle(move, moverSession);

            Assert.AreEqual(10d, mover.Position.x, 0.001);
            Assert.AreEqual(12d, mover.Position.z, 0.001);
            Assert.IsFalse(moverSession.Sent.Exists(body => body is CharDCMoveMessage));

            MessageBody? announcedBody = observerSession.Sent.Find(body => body is CharDCMoveMessage);
            Assert.IsNotNull(announcedBody);
            var announced = (CharDCMoveMessage)announcedBody;
            Assert.AreEqual(mover.Identity.Instance, announced.Identity.Instance);
            Assert.AreEqual(0, announced.Unknown);
            Assert.AreEqual((byte)MovementAction.ForwardStart, announced.MoveType);
            Assert.AreEqual(10f, announced.Coordinates.X);
            Assert.AreEqual(5f, announced.Coordinates.Y);
            Assert.AreEqual(12f, announced.Coordinates.Z);
            Assert.AreEqual(7, announced.Unknown1);
            Assert.AreEqual(1.5f, announced.AuxA);
            Assert.AreEqual(2.5f, announced.AuxB);
        }

        [TestMethod]
        public void ReflectedCharDCMoveForAnotherCharacterIsNotAppliedOrAnnounced()
        {
            var locality = new PlayfieldLocality(954, metaData: null);
            Player mover = TestWorld.CreatePlayer(18, "Mover");
            Player observer = TestWorld.CreatePlayer(19, "Observer");
            var moverSession = new RecordingZoneSession { State = SessionState.InPlay };
            var observerSession = new RecordingZoneSession { State = SessionState.InPlay };
            mover.EnterOnline(moverSession);
            observer.EnterOnline(observerSession);
            locality.RegisterDynel(mover);
            locality.RegisterDynel(observer);
            mover.Position = new AORebirth.Core.Vector.Vector3(1, 2, 3);

            var reflected = new CharDCMoveMessage
            {
                Identity = observer.Identity,
                MoveType = (byte)MovementAction.ForwardStart,
                Heading = new MsgQuaternion { X = 0, Y = 0, Z = 0, W = 1 },
                Coordinates = new MsgVector3 { X = 40, Y = 5, Z = 40 }
            };

            new CharDCMoveMessageHandler().Handle(reflected, moverSession);

            Assert.AreEqual(1d, mover.Position.x, 0.001);
            Assert.AreEqual(3d, mover.Position.z, 0.001);
            Assert.IsFalse(observerSession.Sent.Exists(body => body is CharDCMoveMessage));
        }

        [TestMethod]
        public void SpawnMovesMatchHeldMovement()
        {
            Player player = TestWorld.CreatePlayer(21, "Walker");
            player.Position = new AORebirth.Core.Vector.Vector3(4, 5, 6);
            player.Rotation = new AORebirth.Core.Vector.Quaternion(0, 1, 0, 0);

            Assert.AreEqual(0, player.Motor.BuildSpawnMoves().Count);

            player.Motor.ApplyAction(MovementAction.SwitchToSit);
            Assert.AreEqual(0, player.Motor.BuildSpawnMoves().Count);

            player.Motor.ApplyAction(MovementAction.LeaveSit);
            player.Motor.ApplyAction(MovementAction.StrafeRightStart);
            List<CharDCMoveMessage> strafing = player.Motor.BuildSpawnMoves();
            Assert.AreEqual(1, strafing.Count);
            Assert.AreEqual((byte)MovementAction.StrafeRightStart, strafing[0].MoveType);
            Assert.AreEqual(0, strafing[0].Unknown);
            Assert.AreEqual(player.Identity.Instance, strafing[0].Identity.Instance);
            Assert.AreEqual(4f, strafing[0].Coordinates.X);
            Assert.AreEqual(6f, strafing[0].Coordinates.Z);
        }
    }
}
