namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine.Core.Packets;

    /// <summary>
    /// Exact variable-criterion SpellList payload adapter. Only captured actor identity locations,
    /// ordinary current transport header fields and the accepted SL CanFly removal are patched.
    /// The shared Keeper-only fixed-effect serializer cannot safely re-encode these payloads.
    /// </summary>
    public static class MorphVisualPackets
    {
        public static bool TryBuild(int nanoId, bool remove, Identity actor, int playfieldId,
            bool allowFly, out byte[] packet)
        {
            packet = [];
            if (nanoId is not (82835 or 270542 or 288546)) return false;
            if (actor.Type != IdentityType.CanbeAffected || actor.Instance <= 0 || playfieldId <= 0)
                throw new ArgumentException("Morph wire requires the current character and playfield identities.");
            packet = MorphCaptureWireCatalog.GetWire(nanoId, remove);
            int captured = nanoId == 82835 ? MorphCaptureWireCatalog.SparrowCapturedCharacterInstance
                : MorphCaptureWireCatalog.PhasefrontCapturedCharacterInstance;
            int[] offsets = nanoId == 82835 ? [12, 24, 381, 389]
                : remove ? [12, 24, 241, 249] : [12, 24, 365, 373];
            if (BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(16, 4)) != 0x4D450114)
                throw new InvalidDataException("Captured morph body type changed.");
            foreach (int offset in offsets)
            {
                if (BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset, 4)) != captured
                    || (offset != 12 && BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset - 4, 4)) != (int)IdentityType.CanbeAffected))
                    throw new InvalidDataException("Captured morph actor field changed.");
                BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(offset, 4), actor.Instance);
            }
            // Legacy strips this precise block for Sparrow apply in Shadowlands, never for remove.
            if (nanoId == 82835 && !remove && !allowFly)
            {
                byte[] effect = MorphCaptureWireCatalog.GetCanFlyEffectBlock();
                const int effectOffset = 273;
                if (!packet.AsSpan(effectOffset, effect.Length).SequenceEqual(effect)
                    || BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(29, 4)) != 9 * 0x3F1)
                    throw new InvalidDataException("Captured Sparrow flight block changed.");
                byte[] stripped = new byte[packet.Length - effect.Length];
                packet.AsSpan(0, effectOffset).CopyTo(stripped);
                packet.AsSpan(effectOffset + effect.Length).CopyTo(stripped.AsSpan(effectOffset));
                packet = stripped;
                BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(29, 4), 8 * 0x3F1);
            }
            // Existing ZoneMessageCodec ordinary header: DFDF / N3(10) / Unknown1 / length /
            // current sender playfield / current receiver character. No historical capture sender.
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(0, 2), 0xDFDF);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(6, 2), checked((ushort)packet.Length));
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(8, 4), playfieldId);
            return true;
        }
    }
}
