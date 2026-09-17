namespace AORebirth.World.Collision
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;

    /// <summary>
    /// Reads indoor <c>n3Room_t</c> templates from a style playfield blob (Destinations.dat).
    /// Layout follows N3 <c>RDBPlayfield_t::ReadBlob</c> + <c>FUN_10012803</c>.
    /// </summary>
    internal static class StylePlayfieldRoomReader
    {
        const int WaterBlock = 0x3F1;
        const int MaxZones = 49999;

        public static List<StyleRoomTemplate> Read(byte[] payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            List<StyleRoomTemplate>? best = null;
            int[] prefixes = { 0, 4, 8, 12, 16 };
            int[] headers = ResolveDeserializeOffsets(payload);
            for (int h = 0; h < headers.Length; h++)
            {
                for (int p = 0; p < prefixes.Length; p++)
                {
                    int offset = headers[h] + prefixes[p];
                    if (offset < 0 || offset >= payload.Length)
                        continue;

                    if (TryReadAt(payload, offset, out List<StyleRoomTemplate>? rooms)
                        && rooms != null
                        && rooms.Count > 0
                        && (best == null || rooms.Count > best.Count))
                    {
                        best = rooms;
                    }
                }
            }

            return best ?? new List<StyleRoomTemplate>();
        }

        static bool TryReadAt(byte[] payload, int offset, out List<StyleRoomTemplate>? rooms)
        {
            rooms = null;
            try
            {
                using var stream = new MemoryStream(payload, offset, payload.Length - offset, writable: false);
                using var reader = new BinaryReader(stream);
                int formatVersion = reader.ReadInt32();
                if (formatVersion < 7)
                    return false;

                int instanceId = reader.ReadInt32();
                if (instanceId <= 0)
                    return false;

                stream.Position += 32;
                int tilemapId = reader.ReadInt32();
                reader.ReadInt32();
                int zoneCount = reader.ReadInt32();
                if (zoneCount < 1 || zoneCount > MaxZones)
                    return false;

                if (formatVersion > 8)
                {
                    stream.Position += 20 + 0x18;
                    if (stream.Position > stream.Length)
                        return false;
                }

                bool indoor = instanceId != tilemapId;
                if (!indoor)
                    return false;

                var list = new List<StyleRoomTemplate>(zoneCount);
                for (int i = 0; i < zoneCount; i++)
                {
                    if (!TryReadRoom(reader, formatVersion, i, out StyleRoomTemplate? room) || room == null)
                        return false;
                    list.Add(room);
                }

                rooms = list;
                return true;
            }
            catch
            {
                rooms = null;
                return false;
            }
        }

        static bool TryReadRoom(BinaryReader reader, int formatVersion, int instanceId, out StyleRoomTemplate? room)
        {
            room = null;
            byte flags1 = reader.ReadByte();
            reader.ReadByte();
            ushort minX = reader.ReadUInt16();
            ushort minZ = reader.ReadUInt16();
            ushort maxX = reader.ReadUInt16();
            ushort maxZ = reader.ReadUInt16();
            float posX = reader.ReadSingle();
            float posY = reader.ReadSingle();
            float posZ = reader.ReadSingle();
            if (minX >= maxX || minZ >= maxZ || posX <= 0f || posY < 0f || posZ <= 0f)
                return false;

            ushort doorCount = reader.ReadUInt16();
            for (int i = 0; i < doorCount; i++)
            {
                reader.ReadUInt16();
                reader.ReadUInt16();
            }

            if ((flags1 & 0x80) != 0)
            {
                if (reader.BaseStream.Position + 32 > reader.BaseStream.Length)
                    return false;
                reader.BaseStream.Position += 32;
            }

            int lightmapBytes = reader.ReadInt32() - 4;
            int packed = reader.ReadInt32();
            if (packed != 0)
            {
                if (lightmapBytes < 0 || reader.BaseStream.Position + lightmapBytes > reader.BaseStream.Length)
                    return false;
                reader.BaseStream.Position += lightmapBytes;
            }

            int waterSize = reader.ReadInt32();
            if (waterSize == 0)
            {
                room = new StyleRoomTemplate(
                    instanceId,
                    minX,
                    minZ,
                    maxX,
                    maxZ,
                    new Vector3(posX, posY, posZ),
                    flags1 & 3);
                return TryReadAttractors(reader, formatVersion);
            }

            if (waterSize < 0 || waterSize % WaterBlock != 0)
                return false;

            int waterCount = waterSize / WaterBlock - 1;
            if (!TrySkipWater(reader, waterCount))
                return false;

            room = new StyleRoomTemplate(
                instanceId,
                minX,
                minZ,
                maxX,
                maxZ,
                new Vector3(posX, posY, posZ),
                flags1 & 3);
            return TryReadAttractors(reader, formatVersion);
        }

        static bool TrySkipWater(BinaryReader reader, int waterCount)
        {
            for (int i = 0; i < waterCount; i++)
            {
                int flags = reader.ReadInt32();
                int vertexCount = reader.ReadInt32();
                if ((flags & 1) != 0)
                    vertexCount -= 2;
                if (vertexCount < 0)
                    return false;
                if (vertexCount > 0)
                {
                    long bytes = (long)vertexCount * 12;
                    if (reader.BaseStream.Position + bytes > reader.BaseStream.Length)
                        return false;
                    reader.BaseStream.Position += bytes;
                }

                int indexCount = reader.ReadInt32();
                if (indexCount < 0)
                    return false;
                if (indexCount > 0)
                {
                    long bytes = (long)indexCount * 6;
                    if (reader.BaseStream.Position + bytes > reader.BaseStream.Length)
                        return false;
                    reader.BaseStream.Position += bytes;
                }
            }

            return true;
        }

        static bool TryReadAttractors(BinaryReader reader, int formatVersion)
        {
            if (formatVersion <= 4)
                return true;
            if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                return true;

            int count = reader.ReadInt32();
            if (count < 0 || count > 999)
                return false;

            int per = formatVersion < 6 ? 32 : 44;
            long bytes = (long)count * per;
            if (reader.BaseStream.Position + bytes > reader.BaseStream.Length)
                return false;
            reader.BaseStream.Position += bytes;
            return true;
        }

        static int[] ResolveDeserializeOffsets(byte[] payload)
        {
            if (payload.Length < 8)
                return new[] { 0 };

            uint typeId = unchecked((uint)BitConverter.ToInt32(payload, 0));
            bool looksLikeRdbHeader =
                typeId is >= 0x000F4200 and <= 0x000F42FF
                or >= 0x000F6900 and <= 0x000F69FF
                or 0x000FDE97;

            if (!looksLikeRdbHeader)
                return new[] { 0 };

            return new[] { 12, 8, 0 };
        }
    }
}
