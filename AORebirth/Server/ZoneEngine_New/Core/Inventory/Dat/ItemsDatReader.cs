namespace ZoneEngine_New.Core.Inventory.Dat
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Runtime.Serialization;

    using MsgPack;
    using MsgPack.Serialization;

    internal static class ItemsDatReader
    {
        public static List<DatItemTemplate> Read(string path)
        {
            var result = new List<DatItemTemplate>();

            using Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader binaryReader = new BinaryReader(fileStream);

            byte versionLength = binaryReader.ReadByte();
            if (binaryReader.ReadChars(versionLength).Length != versionLength)
                throw new EndOfStreamException("Truncated items.dat version label.");
            binaryReader.ReadInt32(); // packCount
            int expectedItems = binaryReader.ReadInt32();
            int slices = binaryReader.ReadInt32();
            if (expectedItems < 0 || slices < 0 || slices > (fileStream.Length - fileStream.Position) / sizeof(int))
                throw new InvalidDataException("Invalid item or slice count in the items.dat header.");

            MessagePackSerializer<List<DatItemTemplate>> serializer =
                MessagePackSerializer.Get<List<DatItemTemplate>>();

            for (int i = 0; i < slices; i++)
            {
                long offset = fileStream.Position;
                bool unpacking = false;
                using MemoryStream unpacked = new MemoryStream();
                try
                {
                    int size = binaryReader.ReadInt32();
                    if (size <= 0 || size > fileStream.Length - fileStream.Position)
                        throw new InvalidDataException("Invalid compressed slice size.");
                    byte[] buffer = binaryReader.ReadBytes(size);
                    if (buffer.Length != size)
                        throw new EndOfStreamException("Truncated compressed slice.");

                    using MemoryStream compressed = new MemoryStream(buffer);
                    using ZLibStream zlib = new ZLibStream(compressed, CompressionMode.Decompress);
                    zlib.CopyTo(unpacked);
                    unpacked.Position = 0;
                    unpacking = true;
                    List<DatItemTemplate> slice = serializer.Unpack(unpacked);
                    if (slice == null || unpacked.Position != unpacked.Length)
                        throw new InvalidDataException("Expected one complete MessagePack item array.");
                    result.AddRange(slice);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException
                    or SerializationException or InvalidMessagePackStreamException or MessageTypeException
                    or MessageNotSupportedException or UnpackException)
                {
                    string cursor = unpacking ? $", decompressed offset {unpacked.Position}" : string.Empty;
                    throw new InvalidDataException($"Invalid items.dat slice {i + 1} at file offset {offset}{cursor}: {exception.Message}", exception);
                }
            }

            if (result.Count != expectedItems)
                throw new InvalidDataException($"The items.dat header declares {expectedItems} items but {result.Count} were decoded.");
            if (fileStream.Position != fileStream.Length)
                throw new InvalidDataException("Unexpected data after the last items.dat slice.");
            return result;
        }
    }
}
