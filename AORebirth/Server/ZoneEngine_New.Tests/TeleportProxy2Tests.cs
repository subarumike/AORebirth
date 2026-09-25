namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using AODB.Common.RDBObjects;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.WorldSimulation;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class TeleportProxy2Tests
    {
        const int GridPlayfieldId = 152;
        const int GridDoorIndex = 0;

        [TestMethod]
        public void ParseProxyDestinationPacksPlayfieldDoorInstance()
        {
            var args = new List<object>
            {
                (int)IdentityType.PlayfieldDoor,
                GridPlayfieldId,
                GridDoorIndex,
                0
            };

            Assert.IsTrue(
                PortalDoorLandingResolver.TryParseProxyDestination(
                    args,
                    PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                    recordsReturn: false,
                    out PortalDestination destination));

            Assert.AreEqual(GridPlayfieldId, destination.PlayfieldId);
            Assert.AreEqual(PortalLandingKind.DoorDynel, destination.Kind);
            Assert.AreEqual(unchecked((int)0xC0000098), destination.DoorInstance);
            Assert.AreEqual(PortalDoorLandingResolver.Proxy2EntryDoorClearance, destination.DoorClearance);
            Assert.IsFalse(destination.RecordsReturn);
        }

        [TestMethod]
        public void FullArgumentListNamesDestinationLine()
        {
            var args = new List<object> { (int)IdentityType.PlayfieldDoor, 4530, 0, 0, (int)IdentityType.Playfield3, 397746, 500 };

            Assert.IsTrue(
                PortalDoorLandingResolver.TryParseProxyDestination(
                    args,
                    PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                    recordsReturn: false,
                    out PortalDestination destination));

            Assert.AreEqual(4530, destination.PlayfieldId);
            Assert.AreEqual(PortalLandingKind.DestinationMidpoint, destination.Kind);
            Assert.AreEqual((byte)6, destination.DestinationIndex);
        }

        [TestMethod]
        public void DestinationLineForAnotherPlayfieldKeepsDoorLanding()
        {
            var args = new List<object> { (int)IdentityType.PlayfieldDoor, 4530, 0, 0, (int)IdentityType.Playfield3, 0x0006021C, 500 };

            Assert.IsTrue(
                PortalDoorLandingResolver.TryParseProxyDestination(
                    args,
                    PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                    recordsReturn: false,
                    out PortalDestination destination));

            Assert.AreEqual(PortalLandingKind.DoorDynel, destination.Kind);
        }

        [TestMethod]
        [DataRow(70066, 1, 276.1f, 576.1f)]
        [DataRow(135602, 2, 291.6f, 566.8f)]
        [DataRow(201138, 3, 307.8f, 576.3f)]
        public void JobeReturnRingsLandInFrontOfTheirPlatformRing(int packed, int lineIndex, float x, float z)
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            var args = new List<object> { (int)IdentityType.PlayfieldDoor, 4530, 0, 0, (int)IdentityType.Playfield3, packed };

            Assert.IsTrue(
                PortalDoorLandingResolver.TryParseProxyDestination(
                    args,
                    PortalDoorLandingResolver.ProxyEntryDoorClearance,
                    recordsReturn: true,
                    out PortalDestination destination));

            Assert.AreEqual(PortalLandingKind.DestinationMidpoint, destination.Kind);
            Assert.AreEqual((byte)lineIndex, destination.DestinationIndex);
            Assert.IsFalse(destination.RecordsReturn);
            Assert.IsTrue(PortalDoorLandingResolver.TryResolveMidpointLanding(
                DestinationsCatalog.Instance, 4530, destination.DestinationIndex, out Vector3 landing, out _));
            Assert.AreEqual(x, landing.xf, 0.1f);
            Assert.AreEqual(z, landing.zf, 0.1f);
        }

        [TestMethod]
        public void TeleportProxyDoorArgumentsKeepDoorLandingAndReturn()
        {
            var args = new List<object> { (int)IdentityType.PlayfieldDoor, 952, 0, unchecked((int)0xE7100C1Cu), 100002, 1 };

            Assert.IsTrue(
                PortalDoorLandingResolver.TryParseProxyDestination(
                    args,
                    PortalDoorLandingResolver.ProxyEntryDoorClearance,
                    recordsReturn: true,
                    out PortalDestination destination));

            Assert.AreEqual(PortalLandingKind.DoorDynel, destination.Kind);
            Assert.IsTrue(destination.RecordsReturn);
        }

        [TestMethod]
        [DataRow(540, 0xC004021Cu, 6, 297.1f, 199.8f, 615.85f)]
        [DataRow(730, 0xC00D02DAu, 5, 288.7f, 199.9f, 615.85f)]
        public void DoorsIntoJobePlatformLandOnTheirDestinationLine(
            int sourcePlayfieldId, uint doorInstance, int lineIndex, float x, float y, float z)
        {
            var data = new GameDataStore(new StubLogger());
            DestinationsCatalog.Instance.ConfigureRoot(data.RootPath);
            PlayfieldDynel door = data.GetPlayfieldGeometry(sourcePlayfieldId).Dynels!.Dynels.Single(d =>
                d.IdentityType == (int)IdentityType.Door && unchecked((uint)d.IdentityInstance) == doorInstance);

            Assert.IsTrue(PortalDoorLandingResolver.TryReadPortal(door, out PortalDestination portal));
            Assert.AreEqual(4530, portal.PlayfieldId);
            Assert.AreEqual(PortalLandingKind.DestinationMidpoint, portal.Kind);
            Assert.AreEqual((byte)lineIndex, portal.DestinationIndex);
            Assert.IsTrue(PortalDoorLandingResolver.TryResolveMidpointLanding(
                DestinationsCatalog.Instance, 4530, portal.DestinationIndex, out Vector3 landing, out _));
            Assert.AreEqual(x, landing.xf, 0.1f);
            Assert.AreEqual(y, landing.yf, 0.1f);
            Assert.AreEqual(z, landing.zf, 0.1f);
        }

        [TestMethod]
        public void DaoRouteReplacesRecoveredDestinationLine()
        {
            var route = new AORebirth.Database.Dao.TeleportRoute
            {
                Playfield = 540,
                StatelType = (int)IdentityType.Door,
                StatelInstance = 0xC004021Cu,
                DestinationPlayfield = 4530,
                DestinationType = (int)IdentityType.Door,
                DestinationInstance = 0xC00311B2u
            };
            var data = new GameDataStore(new StubLogger(), new TeleportDestinationCatalog(new[] { route }));
            PlayfieldDynel door = data.GetPlayfieldGeometry(540).Dynels!.Dynels.Single(d =>
                d.IdentityType == (int)IdentityType.Door && unchecked((uint)d.IdentityInstance) == 0xC004021Cu);

            Assert.IsTrue(PortalDoorLandingResolver.TryReadPortal(door, out PortalDestination portal));
            Assert.AreEqual(PortalLandingKind.DoorDynel, portal.Kind);
            Assert.AreEqual(unchecked((int)0xC00311B2u), portal.DoorInstance);
        }

        [TestMethod]
        public void ParseProxyDestinationRejectsNonPlayfieldDoorType()
        {
            var args = new List<object> { (int)IdentityType.Terminal, GridPlayfieldId, GridDoorIndex };

            Assert.IsFalse(
                PortalDoorLandingResolver.TryParseProxyDestination(
                    args,
                    PortalDoorLandingResolver.Proxy2EntryDoorClearance,
                    recordsReturn: false,
                    out _));
        }

        [TestMethod]
        public void LandingInFrontUsesHeadingForwardAndProxy2Clearance()
        {
            // Exit-the-Grid terminal at PF152: identity facing (0,0,0,1) → +Z.
            var position = new Vector3(170.0766f, 4.600221f, 240.9393f);
            var heading = new Quaternion(0f, 0f, 0f, 1f);

            Vector3 landing = PortalDoorLandingResolver.LandingInFront(
                position,
                heading,
                PortalDoorLandingResolver.Proxy2EntryDoorClearance);

            Assert.AreEqual(170.0766f, landing.xf, 0.0001f);
            Assert.AreEqual(4.600221f, landing.yf, 0.0001f);
            Assert.AreEqual(243.4393f, landing.zf, 0.0001f);
        }

        [TestMethod]
        public void NotMarkerInvertsPriorCriteriaForFailureText()
        {
            var spell = new ItemSpell
            {
                FunctionType = (int)FunctionType.Text,
                Requirements =
                [
                    new ItemRequirement
                    {
                        StatNumber = (int)CharacterStat.ComputerLiteracy,
                        Operator = (int)Operator.GreaterThan,
                        Value = 90
                    },
                    new ItemRequirement
                    {
                        StatNumber = 0,
                        Operator = (int)Operator.Not,
                        Value = 0
                    }
                ]
            };

            var high = new StatCollection();
            high.Set(CharacterStat.ComputerLiteracy, 100, StatDetail.Base);
            Assert.IsFalse(spell.MeetsRequirements(high));

            var low = new StatCollection();
            low.Set(CharacterStat.ComputerLiteracy, 50, StatDetail.Base);
            Assert.IsTrue(spell.MeetsRequirements(low));
        }
    }
}
