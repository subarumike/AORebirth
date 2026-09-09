namespace ZoneEngine_New.Core.Inventory.Dat
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Ionic.Zlib;

    using MsgPack.Serialization;

    /// <summary>
    /// MessagePack mirror of the extractor-serializer item template. Identical to
    /// <see cref="DatItemTemplate"/> apart from the trailing DynelType field, which only the
    /// RDB export writes. The two layouts cannot share a type: MessagePack packs these as
    /// arrays, so an extra member makes the whole slice fail to unpack.
    /// </summary>
    [Serializable]
    public sealed class ItemEventsDatTemplate
    {
        public Dictionary<int, int> Attack = new();

        public Dictionary<int, int> Defend = new();

        public int Flags;

        public int ID;

        public int ItemType;

        public int MultipleCount;

        public int Nothing;

        public int Quality;

        public List<int> Relations = new();

        public Dictionary<int, int> Stats = new();

        public List<DatAction> Actions { get; set; } = new();

        public List<DatEvent> Events { get; set; } = new();
    }

    /// <summary>
    /// Reads the item event/action companion file. Only templates that carry events or actions
    /// are returned; the RDB export owns every other field.
    /// </summary>
    internal static class ItemEventsDatReader
    {
        public static Dictionary<int, ItemEventsDatTemplate> Read(string path)
        {
            var result = new Dictionary<int, ItemEventsDatTemplate>();

            using Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader binaryReader = new BinaryReader(fileStream);

            byte versionLength = binaryReader.ReadByte();
            binaryReader.ReadChars(versionLength);
            binaryReader.ReadInt32(); // packCount
            binaryReader.ReadInt32(); // capacity
            int slices = binaryReader.ReadInt32();

            MessagePackSerializer<List<ItemEventsDatTemplate>> serializer =
                MessagePackSerializer.Get<List<ItemEventsDatTemplate>>();

            for (int i = 0; i < slices; i++)
            {
                int size = binaryReader.ReadInt32();
                byte[] buffer = binaryReader.ReadBytes(size);
                if (buffer.Length != size)
                    throw new EndOfStreamException("Unexpected EOF reading ItemEvents.dat slice " + (i + 1));

                using MemoryStream compressed = new MemoryStream(buffer);
                using ZlibStream zlib = new ZlibStream(compressed, CompressionMode.Decompress);
                using MemoryStream unpacked = new MemoryStream();
                zlib.CopyTo(unpacked);
                unpacked.Position = 0;
                List<ItemEventsDatTemplate> slice = serializer.Unpack(unpacked);
                if (slice == null)
                    continue;

                foreach (ItemEventsDatTemplate template in slice)
                {
                    if (template.Events.Count == 0 && template.Actions.Count == 0)
                        continue;

                    result[template.ID] = template;
                }
            }

            return result;
        }
    }
}
