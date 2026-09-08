namespace ZoneEngine_New.Tests
{
    using System;
    using System.Buffers.Binary;
    using System.Security.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine.Core.Packets;
    using ZoneEngine_New.Core.Nanos;

    [TestClass]
    public sealed class MorphVisualPacketTests
    {
        [TestMethod]
        [DataRow(82835, false, 424, "5DCECA4E6590B51D2421AE36189ABE733B9270A453201B02BE8EE5E3CC8A10D6")]
        [DataRow(82835, true, 424, "532A314B43E3555CF271E66EEF6C599E6F536AA8F7EEB29735F21035675E436E")]
        [DataRow(270542, false, 419, "8E74B3146C2853BE7E4E4FBCCB58069DDE0CDA1296EC05FBBA9025A2A9780C51")]
        [DataRow(270542, true, 295, "920D12F0AE52CA7A9D9AB686077F461AF6E443778F4811147C6E52831A2EF897")]
        [DataRow(288546, false, 433, "3EFE3C3224FD4968783AA026CE18D1ABF82C2BFBD60EE03A38A44B1684FAE73A")]
        [DataRow(288546, true, 309, "A08DE61B62C72CEA5B69F03873CE21546D1B9AEBB083AFEB946C607393BE8AE3")]
        public void Accepted_variable_effect_body_is_byte_exact_when_actor_is_unchanged(int nanoId, bool remove, int length, string sourceSha256)
        {
            byte[] template = MorphCaptureWireCatalog.GetWire(nanoId, remove);
            Assert.AreEqual(sourceSha256, Convert.ToHexString(SHA256.HashData(template)));
            int actor = nanoId == 82835 ? MorphCaptureWireCatalog.SparrowCapturedCharacterInstance
                : MorphCaptureWireCatalog.PhasefrontCapturedCharacterInstance;
            Assert.IsTrue(MorphVisualPackets.TryBuild(nanoId, remove, new Identity { Type = IdentityType.CanbeAffected, Instance = actor },
                4582, true, out byte[] packet));
            Assert.AreEqual(length, packet.Length);
            CollectionAssert.AreEqual(template.AsSpan(16).ToArray(), packet.AsSpan(16).ToArray());
            Assert.AreEqual(0xDFDF, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(0, 2)));
            Assert.AreEqual(10, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2, 2)));
            Assert.AreEqual(length, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(6, 2)));
            Assert.AreEqual(4582, BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(8, 4)));
        }

        [TestMethod]
        [DataRow(82835, false)]
        [DataRow(82835, true)]
        [DataRow(270542, false)]
        [DataRow(270542, true)]
        [DataRow(288546, false)]
        [DataRow(288546, true)]
        public void Only_four_proven_actor_offsets_and_current_transport_header_change(int nanoId, bool remove)
        {
            byte[] expected = MorphCaptureWireCatalog.GetWire(nanoId, remove);
            int[] offsets = nanoId == 82835 ? [12, 24, 381, 389] : remove ? [12, 24, 241, 249] : [12, 24, 365, 373];
            foreach (int offset in offsets) BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(offset, 4), 77);
            BinaryPrimitives.WriteUInt16BigEndian(expected.AsSpan(0, 2), 0xDFDF);
            BinaryPrimitives.WriteUInt16BigEndian(expected.AsSpan(6, 2), (ushort)expected.Length);
            BinaryPrimitives.WriteInt32BigEndian(expected.AsSpan(8, 4), 1931);
            Assert.IsTrue(MorphVisualPackets.TryBuild(nanoId, remove, new Identity { Type = IdentityType.CanbeAffected, Instance = 77 },
                1931, true, out byte[] actual));
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Shadowlands_sparrow_apply_removes_only_exact_CanFly_block_and_updates_count_length()
        {
            Assert.AreEqual("F23C78345401E6CCA8332C1F3740D63B281067D7F4E50B3C06B7725D833861BB",
                Convert.ToHexString(SHA256.HashData(MorphCaptureWireCatalog.GetCanFlyEffectBlock())));
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 81 };
            Assert.IsTrue(MorphVisualPackets.TryBuild(82835, false, identity, 4582, true, out var allowed));
            Assert.IsTrue(MorphVisualPackets.TryBuild(82835, false, identity, 4582, false, out var restricted));
            Assert.AreEqual(68, allowed.Length - restricted.Length); Assert.AreEqual(356, restricted.Length);
            Assert.AreEqual(8 * 0x3F1, BinaryPrimitives.ReadInt32BigEndian(restricted.AsSpan(29, 4)));
            CollectionAssert.AreEqual(allowed.AsSpan(33, 240).ToArray(), restricted.AsSpan(33, 240).ToArray());
            CollectionAssert.AreEqual(allowed.AsSpan(341).ToArray(), restricted.AsSpan(273).ToArray());
            Assert.AreEqual(81, BinaryPrimitives.ReadInt32BigEndian(restricted.AsSpan(313, 4)));
            Assert.IsTrue(MorphVisualPackets.TryBuild(82835, true, identity, 4582, false, out var removal));
            Assert.AreEqual(424, removal.Length); // removal keeps the flight termination block
        }

        [TestMethod]
        public void Hoverboard_has_no_invented_SpellList_and_templates_are_immutable_copies()
        {
            Assert.IsFalse(MorphVisualPackets.TryBuild(281569, false, new Identity { Type = IdentityType.CanbeAffected, Instance = 1 },
                4582, true, out var packet)); Assert.AreEqual(0, packet.Length);
            byte[] first = MorphCaptureWireCatalog.GetWire(82835, false); byte original = first[40]; first[40] ^= 0xFF;
            Assert.AreEqual(original, MorphCaptureWireCatalog.GetWire(82835, false)[40]);
        }
    }
}
