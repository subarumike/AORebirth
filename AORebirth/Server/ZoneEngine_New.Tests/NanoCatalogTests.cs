namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using MsgPack.Serialization;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class NanoCatalogTests
    {
        [TestMethod]
        public void Packaged_tracked_nanos_decode_exact_declared_inventory()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "GameData", "nanos.dat");
            Assert.IsTrue(File.Exists(path), "The tracked Datafiles/nanos.dat must be packaged, not a runtime fallback.");
            using var reader = new BinaryReader(File.OpenRead(path));
            reader.ReadBytes(reader.ReadByte()); reader.ReadInt32(); int declared = reader.ReadInt32();
            NanoCatalog catalog = NanoCatalog.Load(path);
            Assert.AreEqual(declared, catalog.Count); Assert.IsTrue(catalog.Count > 0);
            Assert.IsTrue(catalog.TryGet(302365, out var aura)); Assert.AreEqual(302365, aura.Id);
        }

        [TestMethod]
        public void Packaged_specialization_inputs_have_exact_catalog_identity_and_offline_audit_artifact()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "GameData", "nanos.dat");
            NanoCatalog catalog = NanoCatalog.Load(path);
            int[] ids = [82835, 281569, 288546, 270542, 223767, 302365, 300439, 100198, 100194, 154914, 154913, 273292];
            var definitions = new List<NanoDefinition>();
            foreach (int id in ids)
            {
                Assert.IsTrue(catalog.TryGet(id, out var nano), "Missing supported nano " + id);
                definitions.Add(nano);
            }
            using var input = File.OpenRead(path);
            // Ignored build evidence only, never consumed by runtime or used as a data fallback.
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "nanos-specializations.audit.json"),
                JsonSerializer.Serialize(new { Authority = "Tracked Datafiles/nanos.dat; audit only, not activation",
                    SourceSha256 = Convert.ToHexString(SHA256.HashData(input)), Count = catalog.Count,
                    Definitions = definitions.OrderBy(n => n.Id).Select(n => new { n.Id, n.DurationCentiseconds,
                        n.NcuCost, n.Strain, n.NanoCost, n.RangeMeters, n.AttackCentiseconds, n.RechargeCentiseconds,
                        n.Template.Stats, n.Template.Actions, n.Template.SpellList }) },
                    new JsonSerializerOptions { WriteIndented = true }));
        }

        [TestMethod]
        public void NanoFormula_layout_preserves_attributes_and_rejects_duplicate_identity()
        {
            string path = Path.Combine(Path.GetTempPath(), "aorebirth-nanos-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                Write(path, [new DatNanoFormula { ID = 51, Instance = 62, ItemType = 3, Type = 4, flags = 7,
                    Stats = new() { [407] = 42, [75] = 8, [8] = 2000, [54] = 11 } }]);
                var catalog = NanoCatalog.Load(path); Assert.IsTrue(catalog.TryGet(51, out var nano));
                Assert.AreEqual(42, nano.NanoCost); Assert.AreEqual(8, nano.Strain);
                Assert.AreEqual(2000, nano.DurationCentiseconds); Assert.AreEqual(11, nano.NcuCost);
                Assert.AreEqual(7, nano.Template.Flags); Assert.AreEqual(3, nano.Template.ItemType);
                Write(path, [new DatNanoFormula { ID = 51 }, new DatNanoFormula { ID = 51 }]);
                Assert.ThrowsExactly<InvalidDataException>(() => NanoCatalog.Load(path));
            }
            finally { File.Delete(path); }
        }

        [TestMethod]
        public void Truncated_and_extra_package_data_are_rejected()
        {
            string path = Path.Combine(Path.GetTempPath(), "aorebirth-nanos-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                Write(path, [new DatNanoFormula { ID = 1 }]);
                using (var append = new FileStream(path, FileMode.Append)) append.WriteByte(1);
                Assert.ThrowsExactly<InvalidDataException>(() => NanoCatalog.Load(path));
                Write(path, [new DatNanoFormula { ID = 1 }]);
                using (var truncate = new FileStream(path, FileMode.Open, FileAccess.Write)) truncate.SetLength(truncate.Length - 1);
                Assert.ThrowsExactly<InvalidDataException>(() => NanoCatalog.Load(path));
            }
            finally { File.Delete(path); }
        }

        private static void Write(string path, List<DatNanoFormula> formulas)
        {
            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
                MessagePackSerializer.Get<List<DatNanoFormula>>().Pack(zlib, formulas);
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write((byte)1); writer.Write((byte)'1'); writer.Write(500); writer.Write(formulas.Count);
            writer.Write(1); writer.Write((int)compressed.Length); writer.Write(compressed.ToArray());
        }
    }
}
