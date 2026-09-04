namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using System.Threading;

    using AORebirth.Core.GameData;

    using Utility;

    /// <summary>
    /// Reads the extracted GameData tree from the fixed runtime location
    /// {BaseDirectory}\GameData. There is no path search. A missing root keeps
    /// locality on its safe indoor fallback until data is provisioned.
    /// </summary>
    internal static class GameDataLoader
    {
        private static readonly Lock Sync = new();

        /// <summary>
        /// metadata.json is written with camelCase names; case-insensitive matching binds it to
        /// the PascalCase contract. Fields are included so the contract can grow plain fields.
        /// </summary>
        private static readonly JsonSerializerOptions MetaDataJsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly string GameDataRootPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            GameDataPaths.RootFolderName);

        private static readonly string PlayfieldsRootPath = Path.Combine(
            GameDataRootPath,
            GameDataPaths.PlayfieldsFolderName);

        private static readonly Dictionary<int, PlayfieldMetaData?> PlayfieldMetaDataCache =
            new Dictionary<int, PlayfieldMetaData?>();

        private static readonly Dictionary<int, PlayfieldSpawnsData> PlayfieldSpawnsCache =
            new Dictionary<int, PlayfieldSpawnsData>();

        private static readonly JsonSerializerOptions SpawnsJsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        internal static string RootPath => GameDataRootPath;

        internal static string PlayfieldsPath => PlayfieldsRootPath;

        internal static void EnsureRootExists()
        {
            if (!Directory.Exists(GameDataRootPath) || !Directory.Exists(PlayfieldsRootPath))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "GameData playfield metadata is unavailable; locality will use the safe indoor fallback. Root="
                    + GameDataRootPath);
            }
        }

        internal static PlayfieldMetaData? LoadPlayfieldMetaData(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            lock (Sync)
            {
                if (PlayfieldMetaDataCache.TryGetValue(playfieldId, out PlayfieldMetaData? cached))
                {
                    return cached;
                }

                PlayfieldMetaData? loaded = ReadPlayfieldMetaData(playfieldId);
                PlayfieldMetaDataCache[playfieldId] = loaded;
                return loaded;
            }
        }

        /// <summary>
        /// Loads Spawns.json for a playfield. Missing file yields an empty Spawns array (no throw).
        /// </summary>
        internal static PlayfieldSpawnsData LoadPlayfieldSpawns(int playfieldId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playfieldId);

            lock (Sync)
            {
                if (PlayfieldSpawnsCache.TryGetValue(playfieldId, out PlayfieldSpawnsData? cached))
                    return cached;

                PlayfieldSpawnsData loaded = ReadPlayfieldSpawns(playfieldId);
                PlayfieldSpawnsCache[playfieldId] = loaded;
                return loaded;
            }
        }

        private static PlayfieldSpawnsData ReadPlayfieldSpawns(int playfieldId)
        {
            string spawnsPath = Path.Combine(
                GameDataRootPath,
                GameDataPaths.PlayfieldSpawnsRelativePath(playfieldId));

            if (!File.Exists(spawnsPath))
            {
                return new PlayfieldSpawnsData
                {
                    SchemaVersion = PlayfieldSpawnsData.SupportedSchemaVersion,
                    PlayfieldId = playfieldId,
                    Spawns = []
                };
            }

            PlayfieldSpawnsData? data;
            try
            {
                data = JsonSerializer.Deserialize<PlayfieldSpawnsData>(
                    File.ReadAllText(spawnsPath),
                    SpawnsJsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield spawns could not be read: "
                    + spawnsPath
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            if (data == null)
                throw new InvalidDataException("Playfield spawns was empty: " + spawnsPath);

            data.Spawns ??= [];
            if (data.PlayfieldId == 0)
                data.PlayfieldId = playfieldId;

            return data;
        }

        private static PlayfieldMetaData? ReadPlayfieldMetaData(int playfieldId)
        {
            string metadataPath = Path.Combine(
                GameDataRootPath,
                GameDataPaths.PlayfieldMetadataRelativePath(playfieldId));

            if (!File.Exists(metadataPath))
            {
                return null;
            }

            PlayfieldMetaData? metaData;
            try
            {
                metaData = JsonSerializer.Deserialize<PlayfieldMetaData>(
                    File.ReadAllText(metadataPath),
                    MetaDataJsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Playfield metadata could not be read: "
                    + metadataPath
                    + " ("
                    + exception.GetType().Name
                    + ": "
                    + exception.Message
                    + ")",
                    exception);
            }

            if (metaData == null)
            {
                throw new InvalidDataException("Playfield metadata was empty: " + metadataPath);
            }

            if (!metaData.IsValid(out string error))
            {
                throw new InvalidDataException(
                    "Playfield metadata is invalid: " + metadataPath + " (" + error + ")");
            }

            if (metaData.TilemapResource != playfieldId)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "GameData tilemapResource {0} does not match playfield {1} path={2}",
                        metaData.TilemapResource,
                        playfieldId,
                        metadataPath));
            }

            return metaData;
        }
    }
}
