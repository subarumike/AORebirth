namespace AORebirth.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// One SurfaceResource payload and the locality cell it was extracted for.
    /// </summary>
    public struct PlayfieldSurfaceEntry
    {
        public PlayfieldSurfaceEntry(int cellId, byte[] payload)
        {
            CellId = cellId;
            Payload = payload;
        }

        public int CellId { get; set; }

        public byte[] Payload { get; set; }
    }

    /// <summary>
    /// Length-prefixed Surfaces.dat layout written by RDBDataExtractor:
    /// u32 entryCount, then per entry u32 cellId, u32 payloadLength, payload bytes.
    /// <para>
    /// Outdoor playfields store their static geometry as one SurfaceResource per locality cell
    /// rather than a single record for the whole playfield, so Collision.dat's single surface slot
    /// cannot hold it.
    /// </para>
    /// </summary>
    public static class PlayfieldSurfacesDat
    {
        public static byte[] Build(IList<PlayfieldSurfaceEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException("entries");
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    byte[] payload = entries[i].Payload ?? Array.Empty<byte>();
                    writer.Write(entries[i].CellId);
                    writer.Write(payload.Length);
                    writer.Write(payload);
                }

                return stream.ToArray();
            }
        }

        public static List<PlayfieldSurfaceEntry> Parse(byte[] surfacesDat)
        {
            if (surfacesDat == null)
            {
                throw new ArgumentNullException("surfacesDat");
            }

            List<PlayfieldSurfaceEntry> entries = new List<PlayfieldSurfaceEntry>();
            using (MemoryStream stream = new MemoryStream(surfacesDat, writable: false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                if (count < 0)
                {
                    throw new InvalidDataException("Surfaces.dat entry count was invalid.");
                }

                for (int i = 0; i < count; i++)
                {
                    int cellId = reader.ReadInt32();
                    int length = reader.ReadInt32();
                    if (length < 0 || stream.Position + length > surfacesDat.Length)
                    {
                        throw new InvalidDataException(
                            "Surfaces.dat payload length was invalid for cell " + cellId + ".");
                    }

                    byte[] payload = reader.ReadBytes(length);
                    if (payload.Length != length)
                    {
                        throw new InvalidDataException(
                            "Surfaces.dat ended before the payload for cell " + cellId + ".");
                    }

                    entries.Add(new PlayfieldSurfaceEntry(cellId, payload));
                }
            }

            return entries;
        }
    }
}
