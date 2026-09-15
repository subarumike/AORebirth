namespace ZoneEngine_New.Core.Nanos;
using System;
using System.Buffers.Binary;
using System.IO;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.GameData;

/// <summary>Projects editable visual payloads using validated actor patch locations.</summary>
public static class MorphVisualPackets
{
    public static bool TryBuild(int nanoId, bool remove, Identity actor, int playfieldId,
        bool allowFly, out byte[] packet, NanoMechanicCatalog? mechanics = null)
    {
        packet = [];
        var catalog = mechanics ?? NanoMechanicCatalog.LoadDefault();
        if (!catalog.TryGet(nanoId, NanoMechanicKind.Morph, out var definition)
            || (remove ? definition.RemoveVisual : definition.ApplyVisual) is not { } visual) return false;
        if (actor.Type != IdentityType.CanbeAffected || actor.Instance <= 0 || playfieldId <= 0)
            throw new ArgumentException("Morph projection requires current character and playfield identities.");
        packet = Convert.FromHexString(visual.Hex);
        if (BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(16, 4)) != 0x4D450114)
            throw new InvalidDataException("Invalid morph message type.");
        foreach (int offset in visual.ActorOffsets)
        {
            if (BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset, 4)) != visual.SourceActorId
                || (offset != 12 && (offset < 4 || BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(offset - 4, 4)) != (int)IdentityType.CanbeAffected)))
                throw new InvalidDataException("Invalid morph actor patch location.");
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(offset, 4), actor.Instance);
        }
        if (!remove && !allowFly && visual.FlightBlock is { } block)
        {
            byte[] effect = Convert.FromHexString(block.Hex);
            if (!packet.AsSpan(block.Offset, effect.Length).SequenceEqual(effect)
                || BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(block.CountOffset, 4)) != block.CountWithBlock)
                throw new InvalidDataException("Invalid conditional morph block.");
            byte[] stripped = new byte[packet.Length - effect.Length];
            packet.AsSpan(0, block.Offset).CopyTo(stripped);
            packet.AsSpan(block.Offset + effect.Length).CopyTo(stripped.AsSpan(block.Offset));
            packet = stripped;
            int countOffset = block.CountOffset >= block.Offset + effect.Length ? block.CountOffset - effect.Length : block.CountOffset;
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(countOffset, 4), block.CountWithoutBlock);
        }
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(0, 2), 0xDFDF);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(6, 2), checked((ushort)packet.Length));
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(8, 4), playfieldId);
        return true;
    }
}
