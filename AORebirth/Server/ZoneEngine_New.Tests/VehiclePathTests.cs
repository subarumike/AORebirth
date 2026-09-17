namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Movement;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    [TestClass]
    public sealed class VehiclePathTests
    {
        [TestMethod]
        public void Advance_MapsDistanceAlongPolyline()
        {
            var path = new VehiclePath();
            path.Set(
                [
                    new Vector3(0, 0, 0),
                    new Vector3(4, 0, 0),
                    new Vector3(4, 0, 3)
                ],
                speed: 2f);

            Assert.IsTrue(path.Advance(2f, out Vector3 mid, out Vector3 dir));
            Assert.AreEqual(4f, (float)mid.x, 0.001f);
            Assert.AreEqual(0f, (float)mid.z, 0.001f);
            Assert.AreEqual(1f, (float)dir.x, 0.001f);

            Assert.IsFalse(path.Advance(1.5f, out Vector3 end, out _));
            Assert.AreEqual(4f, (float)end.x, 0.001f);
            Assert.AreEqual(3f, (float)end.z, 0.001f);
            Assert.IsFalse(path.IsActive);
        }
    }
}