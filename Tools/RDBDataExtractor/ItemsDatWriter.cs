namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    using Ionic.Zlib;

    using MsgPack.Serialization;

    using ZoneEngine_New.Core.Inventory.Dat;

    internal static class ItemsDatWriter
    {
        internal const string VersionLabel = "RDBDataExtractor";

        internal const int PackCount = 10000;

        internal static void Write(string path, List<DatItemTemplate> templates)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("items.dat path is required.", "path");
            if (templates == null)
                throw new ArgumentNullException("templates");

            MessagePackSerializer<List<DatItemTemplate>> serializer =
                MessagePackSerializer.Get<List<DatItemTemplate>>();

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (Stream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var binaryWriter = new BinaryWriter(fileStream))
            {
                byte[] versionBuffer = Encoding.ASCII.GetBytes(VersionLabel);
                binaryWriter.Write((byte)versionBuffer.Length);
                binaryWriter.Write(versionBuffer, 0, versionBuffer.Length);
                binaryWriter.Write(PackCount);
                binaryWriter.Write(templates.Count);

                int slices = templates.Count == 0
                    ? 0
                    : (templates.Count + PackCount - 1) / PackCount;
                binaryWriter.Write(slices);

                for (int slice = 0; slice < slices; slice++)
                {
                    int offset = slice * PackCount;
                    int take = Math.Min(PackCount, templates.Count - offset);
                    var chunk = new List<DatItemTemplate>(take);
                    for (int i = 0; i < take; i++)
                        chunk.Add(templates[offset + i]);

                    using (var unpacked = new MemoryStream())
                    {
                        serializer.Pack(unpacked, chunk);
                        unpacked.Position = 0;
                        using (var compressed = new MemoryStream())
                        {
                            using (var zlib = new ZlibStream(compressed, CompressionMode.Compress, true))
                                unpacked.CopyTo(zlib);

                            byte[] buffer = compressed.ToArray();
                            binaryWriter.Write(buffer.Length);
                            binaryWriter.Write(buffer, 0, buffer.Length);
                        }
                    }
                }
            }
        }
    }
}
