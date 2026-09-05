namespace AORebirth.Core.GameData
{
    using System;
    using System.Globalization;
    using System.IO;

    /// <summary>
    /// Path conventions for the checked-in GameData tree.
    /// Relative segments only: the extractor resolves them against its output root and
    /// the server resolves them against its runtime base directory.
    /// </summary>
    public static class GameDataPaths
    {
        public const string RootFolderName = "GameData";

        public const string PlayfieldsFolderName = "Playfields";

        public const string MetadataFileName = "metadata.json";

        public const string DistrictsFileName = "Districts.json";

        public const string SpawnsFileName = "Spawns.json";

        public const string MobTemplatesFileName = "MobTemplates.json";

        public const string LootTableFileName = "LootTable.json";

        public const string MonsterDataFileName = "MonsterData.json";

        public const string WallsFileName = "Walls.dat";

        public const string DynelsFileName = "Dynels.dat";

        public const string CollisionFileName = "Collision.dat";

        public static string PlayfieldRelativeDirectory(int playfieldId)
        {
            if (playfieldId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    "playfieldId",
                    playfieldId,
                    "A positive playfield id is required.");
            }

            return Path.Combine(
                PlayfieldsFolderName,
                playfieldId.ToString(CultureInfo.InvariantCulture));
        }

        public static string PlayfieldMetadataRelativePath(int playfieldId)
        {
            return Path.Combine(PlayfieldRelativeDirectory(playfieldId), MetadataFileName);
        }

        public static string PlayfieldDistrictsRelativePath(int playfieldId)
        {
            return Path.Combine(PlayfieldRelativeDirectory(playfieldId), DistrictsFileName);
        }

        public static string PlayfieldSpawnsRelativePath(int playfieldId)
        {
            return Path.Combine(PlayfieldRelativeDirectory(playfieldId), SpawnsFileName);
        }

        public static string PlayfieldWallsRelativePath(int playfieldId)
        {
            return Path.Combine(PlayfieldRelativeDirectory(playfieldId), WallsFileName);
        }

        public static string PlayfieldDynelsRelativePath(int playfieldId)
        {
            return Path.Combine(PlayfieldRelativeDirectory(playfieldId), DynelsFileName);
        }

        public static string PlayfieldCollisionRelativePath(int playfieldId)
        {
            return Path.Combine(PlayfieldRelativeDirectory(playfieldId), CollisionFileName);
        }
    }
}
