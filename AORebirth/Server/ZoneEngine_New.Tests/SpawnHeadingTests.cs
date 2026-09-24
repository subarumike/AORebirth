namespace ZoneEngine_New.Tests
{
    using System;

    using AORebirth.Core.Vector;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Playfield;

    [TestClass]
    public sealed class SpawnHeadingTests
    {
        [TestMethod]
        public void ShortsAreDegreesConvertedLikeTheClient()
        {
            // Playfield 4582, ICC Shuttleport, Dreadknot: e2 00 1e 00 -> 226, 30.
            Assert.AreEqual(3.944f, SpawnHeading.ToRadians(226), 0.001f);
            Assert.AreEqual(0.524f, SpawnHeading.ToRadians(30), 0.001f);

            Quaternion facing = SpawnHeading.FromFacingDegrees(226);
            Assert.AreEqual(226.0, YawDegrees(facing), 0.01);
            Assert.AreEqual(0f, facing.xf, 1e-5f);
            Assert.AreEqual(0f, facing.zf, 1e-5f);

            double length = Math.Sqrt((226.0 * 226.0) + (30.0 * 30.0));
            Assert.AreNotEqual(226.0 / length, facing.y, 0.05);
        }

        [TestMethod]
        public void WidthIsTheFacingConeAroundTheMidAngle()
        {
            Quaternion centre = SpawnHeading.Sample(226, 30, 0.5);
            Quaternion low = SpawnHeading.Sample(226, 30, 0.0);
            Quaternion high = SpawnHeading.Sample(226, 30, 1.0);

            Assert.AreEqual(226.0, YawDegrees(centre), 0.02);
            Assert.AreEqual(211.0, YawDegrees(low), 0.02);
            Assert.AreEqual(241.0, YawDegrees(high), 0.02);
        }

        [TestMethod]
        public void ZeroFacingIsIdentityYaw()
        {
            Quaternion facing = SpawnHeading.FromFacingDegrees(0);
            Assert.AreEqual(0f, facing.yf, 1e-6f);
            Assert.AreEqual(1f, facing.wf, 1e-6f);

            Quaternion sampled = SpawnHeading.Sample(0, 0, 0.25);
            Assert.AreEqual(0.0, YawDegrees(sampled), 0.01);
        }

        static double YawDegrees(Quaternion heading)
        {
            return heading.yaw * (180.0 / Math.PI);
        }
    }
}
