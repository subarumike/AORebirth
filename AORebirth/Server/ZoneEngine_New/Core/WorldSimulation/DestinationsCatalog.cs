namespace ZoneEngine_New.Core.WorldSimulation
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using AODB.Common.RDBObjects;

    using AORebirth.Core.GameData;

    /// <summary>
    /// Process-wide sparse Destinations maps keyed by playfield id (wall landing lines).
    /// Loaded lazily from Destinations.dat (raw RDBPlayfield / resource 1000001 payload).
    /// </summary>
    public sealed class DestinationsCatalog
    {
        public static DestinationsCatalog Instance { get; } = new DestinationsCatalog();

        readonly object _sync = new();
        readonly Dictionary<int, Dictionary<byte, PlayfieldDestination>> _byPlayfield = new();
        string? _rootPath;

        public void ConfigureRoot(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            lock (_sync)
                _rootPath = gameDataRoot;
        }

        public bool TryGetDestination(
            int playfieldId,
            byte destinationIndex,
            out PlayfieldDestination? destination)
        {
            destination = null;
            Dictionary<byte, PlayfieldDestination>? map = GetOrLoad(playfieldId);
            if (map == null)
                return false;

            return map.TryGetValue(destinationIndex, out destination) && destination != null;
        }

        public void Register(int playfieldId, Dictionary<byte, PlayfieldDestination> destinations)
        {
            ArgumentNullException.ThrowIfNull(destinations);
            lock (_sync)
                _byPlayfield[playfieldId] = destinations;
        }

        Dictionary<byte, PlayfieldDestination>? GetOrLoad(int playfieldId)
        {
            lock (_sync)
            {
                if (_byPlayfield.TryGetValue(playfieldId, out Dictionary<byte, PlayfieldDestination>? cached))
                    return cached;

                if (string.IsNullOrEmpty(_rootPath))
                    return null;

                string path = Path.Combine(
                    _rootPath,
                    GameDataPaths.PlayfieldDestinationsRelativePath(playfieldId));
                Dictionary<byte, PlayfieldDestination> loaded = TryLoadFromFile(path);
                _byPlayfield[playfieldId] = loaded;
                return loaded;
            }
        }

        static Dictionary<byte, PlayfieldDestination> TryLoadFromFile(string path)
        {
            var result = new Dictionary<byte, PlayfieldDestination>();
            if (!File.Exists(path))
                return result;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0)
                    return result;

                RDBPlayfield? record = DeserializePlayfield(bytes);
                if (record?.Destinations == null)
                    return result;

                foreach (KeyValuePair<byte, Destination> pair in record.Destinations)
                {
                    Destination src = pair.Value;
                    if (src == null)
                        continue;

                    result[pair.Key] = new PlayfieldDestination
                    {
                        DestinationId = src.DestinationId,
                        StartX = src.StartX,
                        StartY = src.StartY,
                        StartZ = src.StartZ,
                        EndX = src.EndX,
                        EndY = src.EndY,
                        EndZ = src.EndZ
                    };
                }
            }
            catch
            {
                // Missing/corrupt Destinations.dat → empty map (wall landings fail soft).
            }

            return result;
        }

        static RDBPlayfield? DeserializePlayfield(byte[] payload)
        {
            RDBPlayfield? best = null;
            long bestRemaining = long.MaxValue;
            int[] offsets = ResolveDeserializeOffsets(payload);
            for (int i = 0; i < offsets.Length; i++)
            {
                int offset = offsets[i];
                if (offset < 0 || offset >= payload.Length)
                    continue;

                try
                {
                    var record = new RDBPlayfield();
                    using var stream = new MemoryStream(payload, offset, payload.Length - offset, writable: false);
                    using var reader = new BinaryReader(stream);
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
                catch
                {
                    // Wrong header offset is common; try the next one.
                }
            }

            return best;
        }

        /// <summary>
        /// RDBDataExtractor may leave type+id+version (12 bytes) on Destinations.dat.
        /// Same offset preference as GameDataStore geometry loads.
        /// </summary>
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
