namespace AORebirth.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// One water mesh extracted from RDBPlayfield.GlobalWaterData or RoomTemplate.Water.
    /// RoomIndex is -1 for playfield-global water.
    /// </summary>
    public struct PlayfieldWaterEntry
    {
        public PlayfieldWaterEntry(int roomIndex, int flags, float[] vertices, short[] triangles)
        {
            RoomIndex = roomIndex;
            Flags = flags;
            Vertices = vertices;
            Triangles = triangles;
        }

        public int RoomIndex { get; set; }

        public int Flags { get; set; }

        public float[] Vertices { get; set; }

        public short[] Triangles { get; set; }
    }

    /// <summary>
    /// Length-prefixed Water.dat layout written by RDBDataExtractor:
    /// u32 schemaVersion, i32 playfieldId, u32 entryCount, then per entry
    /// i32 roomIndex, i32 flags, i32 vertexFloatCount, vertex floats, i32 triangleCount, i16 triangles.
    /// </summary>
    public static class PlayfieldWaterDat
    {
        public const int SupportedSchemaVersion = 1;

        public static byte[] Build(int playfieldId, IList<PlayfieldWaterEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException("entries");

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(SupportedSchemaVersion);
                writer.Write(playfieldId);
                writer.Write(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    float[] vertices = entries[i].Vertices ?? Array.Empty<float>();
                    short[] triangles = entries[i].Triangles ?? Array.Empty<short>();
                    writer.Write(entries[i].RoomIndex);
                    writer.Write(entries[i].Flags);
                    writer.Write(vertices.Length);
                    for (int v = 0; v < vertices.Length; v++)
                        writer.Write(vertices[v]);
                    writer.Write(triangles.Length);
                    for (int t = 0; t < triangles.Length; t++)
                        writer.Write(triangles[t]);
                }

                return stream.ToArray();
            }
        }

        public static List<PlayfieldWaterEntry> Parse(byte[] waterDat, out int playfieldId)
        {
            if (waterDat == null)
                throw new ArgumentNullException("waterDat");

            playfieldId = 0;
            List<PlayfieldWaterEntry> entries = new List<PlayfieldWaterEntry>();
            using (MemoryStream stream = new MemoryStream(waterDat, writable: false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                if (version != SupportedSchemaVersion)
                {
                    throw new InvalidDataException(
                        "Water.dat schemaVersion must be "
                        + SupportedSchemaVersion
                        + " but was "
                        + version
                        + ".");
                }

                playfieldId = reader.ReadInt32();
                int count = reader.ReadInt32();
                if (count < 0)
                    throw new InvalidDataException("Water.dat entry count was invalid.");

                for (int i = 0; i < count; i++)
                {
                    int roomIndex = reader.ReadInt32();
                    int flags = reader.ReadInt32();
                    int vertexCount = reader.ReadInt32();
                    if (vertexCount < 0 || vertexCount % 3 != 0)
                    {
                        throw new InvalidDataException(
                            "Water.dat vertex count was invalid for room " + roomIndex + ".");
                    }

                    float[] vertices = new float[vertexCount];
                    for (int v = 0; v < vertexCount; v++)
                        vertices[v] = reader.ReadSingle();

                    int triangleCount = reader.ReadInt32();
                    if (triangleCount < 0)
                    {
                        throw new InvalidDataException(
                            "Water.dat triangle count was invalid for room " + roomIndex + ".");
                    }

                    short[] triangles = new short[triangleCount];
                    for (int t = 0; t < triangleCount; t++)
                        triangles[t] = reader.ReadInt16();

                    entries.Add(new PlayfieldWaterEntry(roomIndex, flags, vertices, triangles));
                }
            }

            return entries;
        }
    }
}
