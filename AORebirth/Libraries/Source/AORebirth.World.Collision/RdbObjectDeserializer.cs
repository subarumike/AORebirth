namespace AORebirth.World.Collision
{
    using System;
    using System.IO;

    using AODB.Common.RDBObjects;

    /// <summary>
    /// Deserializes RDB bodies written by RDBDataExtractor (header-stripped or GetRaw-prefixed).
    /// Prefers the offset that consumes the most of the payload.
    /// </summary>
    internal static class RdbObjectDeserializer
    {
        public static T Deserialize<T>(byte[] payload, string sourcePath)
            where T : RDBObject, new()
        {
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(sourcePath);

            Exception? lastFailure = null;
            T? best = null;
            long bestRemaining = long.MaxValue;
            int[] offsets = ResolveDeserializeOffsets(payload);
            for (int i = 0; i < offsets.Length; i++)
            {
                int offset = offsets[i];
                if (offset < 0 || offset >= payload.Length)
                    continue;

                try
                {
                    T record = new();
                    // Surfaces.dat / Collision.dat strip the RDB type+id+version header.
                    // AODB SurfaceResource.Deserialize requires RecordVersion 5 on the instance.
                    if (record is SurfaceResource surface)
                        surface.RecordVersion = 5;

                    using MemoryStream stream = new(payload, offset, payload.Length - offset, writable: false);
                    using BinaryReader reader = new(stream);
                    record.Deserialize(reader);
                    long remaining = stream.Length - stream.Position;
                    if (remaining < bestRemaining)
                    {
                        best = record;
                        bestRemaining = remaining;
                        if (remaining == 0)
                            return record;
                    }
                }
                catch (Exception exception)
                {
                    lastFailure = exception;
                }
            }

            if (best != null)
                return best;

            throw new InvalidDataException(
                "Playfield geometry could not be deserialized: "
                + sourcePath
                + " ("
                + (lastFailure?.GetType().Name ?? "Error")
                + ": "
                + (lastFailure?.Message ?? "no viable offset")
                + ")",
                lastFailure);
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
