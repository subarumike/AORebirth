namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
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
            Assert.IsFalse(StatModifierSpells.MeetsRequirements(spell, high));

            var low = new StatCollection();
            low.Set(CharacterStat.ComputerLiteracy, 50, StatDetail.Base);
            Assert.IsTrue(StatModifierSpells.MeetsRequirements(spell, low));
        }
    }
}
