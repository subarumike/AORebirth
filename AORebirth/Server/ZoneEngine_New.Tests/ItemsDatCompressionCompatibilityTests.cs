namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using MsgPack.Serialization;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
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
                List<DatItemTemplate> items = ItemsDatReader.Read(path);
                Assert.AreEqual(0, items.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ValidItemDataKeepsExistingTemplatePrecedence()
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory();
            try
            {
                WriteItemsDat(directory, 2,
                    Pack(new DatItemTemplate { ID = 1, Name = "Initial fixture item", Quality = 100 }),
                    Pack(new DatItemTemplate { ID = 1, Name = "Fixture item", Quality = 125 }));
                var catalog = LoadCatalog(directory);
                Assert.AreEqual(1, catalog.Templates.Count);
                Assert.AreEqual("Fixture item", catalog.Require(1).Name);
                Assert.AreEqual(125, catalog.Require(1).Quality);
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MissingOrEmptyRequiredItemDataFailsCatalogConstruction(bool present)
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory();
            try
            {
                if (present)
                    WriteItemsDat(directory, 0);
                var failure = Assert.ThrowsExactly<InvalidDataException>(() => LoadCatalog(directory));
                StringAssert.Contains(failure.Message, Path.Combine(directory.FullName, "items.dat"));
                if (present)
                    StringAssert.Contains(failure.Message, "no item templates");
                else
                    Assert.IsInstanceOfType<FileNotFoundException>(failure.InnerException);
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }

        [TestMethod]
        public void ItemCountMismatchFailsCatalogConstruction()
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory();
            try
            {
                WriteItemsDat(directory, 2, Pack(new DatItemTemplate { ID = 1 }));
                var failure = Assert.ThrowsExactly<InvalidDataException>(() => LoadCatalog(directory));
                StringAssert.Contains(failure.Message, "declares 2 items but 1 were decoded");
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }

        [TestMethod]
        public void GenuineDuplicateSpellStatFailsCatalogConstructionWithSliceContext()
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory();
            try
            {
                byte[] corrupted = Pack(new DatItemTemplate
                {
                    ID = 2,
                    Rdb = new DatRdbTemplate
                    {
                        Events = { [1] = [new DatSpell { Stats = { [725] = 1, [726] = 2 } }] }
                    }
                });
                byte[] originalMap = [0x82, 0xd1, 0x02, 0xd5, 0x01, 0xd1, 0x02, 0xd6, 0x02];
                int offset = corrupted.AsSpan().IndexOf(originalMap);
                Assert.IsTrue(offset >= 0, "Fixture must contain the original two-key MessagePack map.");
                corrupted[offset + 7] = 0xd5;
                WriteItemsDat(directory, 2, Pack(new DatItemTemplate { ID = 1 }), corrupted);

                var failure = Assert.ThrowsExactly<InvalidDataException>(() => LoadCatalog(directory));
                StringAssert.Contains(failure.Message, Path.Combine(directory.FullName, "items.dat"));
                StringAssert.Contains(failure.Message, "slice 2 at file offset");
                StringAssert.Contains(failure.Message, "decompressed offset");
                StringAssert.Contains(failure.Message, "725");
                Assert.IsInstanceOfType<ArgumentException>(failure.InnerException?.InnerException);
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }

        private static ItemTemplateCatalog LoadCatalog(DirectoryInfo directory)
            => new ItemTemplateCatalog(new StubGameData(HashItemCatalog.Parse("{}"), rootPath: directory.FullName), new StubLogger());

        private static byte[] Pack(DatItemTemplate template)
        {
            using var stream = new MemoryStream();
            MessagePackSerializer.Get<List<DatItemTemplate>>().Pack(stream, [template]);
            return stream.ToArray();
        }

        private static void WriteItemsDat(DirectoryInfo directory, int count, params byte[][] slices)
        {
            using var writer = new BinaryWriter(File.Create(Path.Combine(directory.FullName, "items.dat")));
            writer.Write((byte)0);
            writer.Write(10000);
            writer.Write(count);
            writer.Write(slices.Length);
            foreach (byte[] slice in slices)
            {
                using var compressed = new MemoryStream();
                using (var zlib = new ZLibStream(compressed, CompressionMode.Compress, leaveOpen: true))
                    zlib.Write(slice);
                writer.Write((int)compressed.Length);
                writer.Write(compressed.ToArray());
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
