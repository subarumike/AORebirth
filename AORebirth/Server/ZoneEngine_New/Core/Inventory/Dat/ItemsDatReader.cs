namespace ZoneEngine_New.Core.Inventory.Dat
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Ionic.Zlib;

    using MsgPack.Serialization;

    internal static class ItemsDatReader
    {
        public static List<DatItemTemplate> Read(string path)
        {
            var result = new List<DatItemTemplate>();

            using Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader binaryReader = new BinaryReader(fileStream);

            byte versionLength = binaryReader.ReadByte();
            binaryReader.ReadChars(versionLength);
            binaryReader.ReadInt32(); // packCount
            int capacity = binaryReader.ReadInt32();
            int slices = binaryReader.ReadInt32();
            result.Capacity = capacity;

            MessagePackSerializer<List<DatItemTemplate>> serializer =
                MessagePackSerializer.Get<List<DatItemTemplate>>();

            for (int i = 0; i < slices; i++)
            {
                int size = binaryReader.ReadInt32();
                byte[] buffer = binaryReader.ReadBytes(size);
                if (buffer.Length != size)
                    throw new EndOfStreamException("Unexpected EOF reading items.dat slice " + (i + 1));

                using MemoryStream compressed = new MemoryStream(buffer);
                using ZlibStream zlib = new ZlibStream(compressed, CompressionMode.Decompress);
                using MemoryStream unpacked = new MemoryStream();
                zlib.CopyTo(unpacked);
                unpacked.Position = 0;
                List<DatItemTemplate> slice = serializer.Unpack(unpacked);
                if (slice != null)
                    result.AddRange(slice);
            }

            return result;
        }
    }
}
