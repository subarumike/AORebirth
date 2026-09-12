namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ZoneEngine_New.Core.Inventory.Dat;

    [TestClass]
    public sealed class ItemsDatCompressionCompatibilityTests
    {
        // Frozen with DotNetZip 1.16.0 Ionic.Zlib.ZlibStream.CompressBuffer(new byte[] { 0x90 }).
        // 0x90 is the MessagePack empty-array marker; the decoder must retain zlib framing.
        private const string LegacyEmptyMessagePackSlice = "eNqbAAAAkQCR";

        [TestMethod]
        public void ReaderAcceptsLegacyDotNetZipSliceWithoutTheLegacyDependency()
        {
            string path = Path.GetTempFileName();
            try
            {
                byte[] compressed = Convert.FromBase64String(LegacyEmptyMessagePackSlice);
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write((byte)0); // version-label length
                    writer.Write(10000); // pack size
                    writer.Write(0); // item count
                    writer.Write(1); // slice count
                    writer.Write(compressed.Length);
                    writer.Write(compressed);
                }
                var reader = typeof(DatItemTemplate).Assembly.GetType(
                    "ZoneEngine_New.Core.Inventory.Dat.ItemsDatReader", throwOnError: true)!;
                var items = (List<DatItemTemplate>)reader.GetMethod("Read")!.Invoke(null, new object[] { path })!;
                Assert.AreEqual(0, items.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Windows1252RemainsAvailableFromTheNet10Runtime()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Assert.AreEqual("\u20ac", Encoding.GetEncoding(1252).GetString(new byte[] { 0x80 }));
        }
    }
}
