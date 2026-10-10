namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class DoorLineOfSightTests
    {
        static Door DoorAt(int flags) => new(new Identity { Type = IdentityType.Door, Instance = 1 }, 0, flags, 0)
        {
            Position = new Vector3(10, 1, 10),
            Rotation = Door.Heading(90)
        };

        [TestMethod]
        public void ClosedDoor_BlocksOnlySegmentsThroughItsDoorway()
        {
            Door closed = DoorAt(0);

            // Yaw 90 faces +X, so the doorway lies in the X = 10 plane.
            Assert.IsTrue(closed.BlocksSegment(5f, 2.6f, 10f, 15f, 2.6f, 10f), "straight through the doorway");
            Assert.IsTrue(closed.BlocksSegment(15f, 2.6f, 11f, 5f, 2.6f, 9f), "through it from the other side, at an angle");
            Assert.IsFalse(closed.BlocksSegment(5f, 2.6f, 10f, 9f, 2.6f, 10f), "both ends on the same side");
            Assert.IsFalse(closed.BlocksSegment(5f, 2.6f, 20f, 15f, 2.6f, 20f), "crosses the plane well beside the doorway");
            Assert.IsFalse(DoorAt(Door.OpenFlag).BlocksSegment(5f, 2.6f, 10f, 15f, 2.6f, 10f), "open door");
        }
    }
}
