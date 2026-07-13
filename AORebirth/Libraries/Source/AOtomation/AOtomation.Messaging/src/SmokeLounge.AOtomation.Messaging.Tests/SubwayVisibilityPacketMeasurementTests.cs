namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class SubwayVisibilityPacketMeasurementTests
    {
        [TestMethod]
        public void SerializedSizeMeasurementDoesNotAlterPacketBytes()
        {
            byte[] payload =
                {
                    0xDF, 0xDF, 0x00, 0x01, 0x12, 0x34, 0x56, 0x78,
                    0x3B, 0x1D, 0x22, 0x68, 0x00, 0x00, 0x00, 0x00
                };
            byte[] before = (byte[])payload.Clone();

            int measured = SubwayVisibilityPacketMeasurement.MeasureSerializedBytes(payload);

            Assert.AreEqual(payload.Length, measured);
            CollectionAssert.AreEqual(before, payload);
            Assert.AreEqual(0, SubwayVisibilityPacketMeasurement.MeasureSerializedBytes(null));
        }
    }
}
