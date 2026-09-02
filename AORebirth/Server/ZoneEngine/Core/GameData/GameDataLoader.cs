namespace ZoneEngine.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Web.Script.Serialization;

    using AORebirth.Core.GameData;

    using Utility;

    /// <summary>
    /// Reads the checked-in GameData tree from the fixed runtime location
    /// {BaseDirectory}\GameData. There is no path search: a missing root is a
    /// deployment error and fails at startup.
    /// </summary>
    internal static class GameDataLoader
    {
        private static readonly object Sync = new object();

        private static readonly string GameDataRootPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            GameDataPaths.RootFolderName);

        private static readonly string PlayfieldsRootPath = Path.Combine(
            GameDataRootPath,
            GameDataPaths.PlayfieldsFolderName);

        /// <summary>
        /// Caches parsed metadata per playfield id, including absent playfields, so that
        /// frequently created instances do not probe the file system on every zoning.
        /// </summary>
        private static readonly Dictionary<int, PlayfieldMetaData> PlayfieldMetaDataCache =
            new Dictionary<int, PlayfieldMetaData>();

        internal static string RootPath
        {
            get { return GameDataRootPath; }
        }

        internal static string PlayfieldsPath
        {
            get { return PlayfieldsRootPath; }
        }

        /// <summary>
        /// Fails immediately when the GameData tree is not deployed beside the engine.
        /// </summary>
        internal static void EnsureRootExists()
        {
            if (!Directory.Exists(GameDataRootPath))
            {
                throw new DirectoryNotFoundException(
                    "GameData root directory was not found: " + GameDataRootPath);
            }

            if (!Directory.Exists(PlayfieldsRootPath))
            {
                throw new DirectoryNotFoundException(
                    "GameData playfield directory was not found: " + PlayfieldsRootPath);
            }
        }

        /// <summary>
        /// Returns the playfield metadata, or null when the playfield has no extracted
        /// ground tilemap. Throws when metadata exists but cannot be read or is invalid.
        /// </summary>
        internal static PlayfieldMetaData LoadPlayfieldMetaData(int playfieldId)
        {
            if (playfieldId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "playfieldId",
                    playfieldId,
                    "A positive playfield id is required.");
            }

            lock (Sync)
            {
                PlayfieldMetaData cached;
                if (PlayfieldMetaDataCache.TryGetValue(playfieldId, out cached))
                {
                    return cached;
                }

                PlayfieldMetaData loaded = ReadPlayfieldMetaData(playfieldId);
                PlayfieldMetaDataCache[playfieldId] = loaded;
                return loaded;
            }
        }

        private static PlayfieldMetaData ReadPlayfieldMetaData(int playfieldId)
        {
            string metadataPath = Path.Combine(
                GameDataRootPath,
                GameDataPaths.PlayfieldMetadataRelativePath(playfieldId));

            if (!File.Exists(metadataPath))
            {
                // Only playfields with an RDB ground tilemap are extracted. Indoor dungeons,
                // mission instances and private cities have none, and run the indoor layout.
                return null;
            }

            PlayfieldMetaData metaData;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue
                };
                metaData = serializer.Deserialize<PlayfieldMetaData>(File.ReadAllText(metadataPath));
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

            string error;
            if (!metaData.IsValid(out error))
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
