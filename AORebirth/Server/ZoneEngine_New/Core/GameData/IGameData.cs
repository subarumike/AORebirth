namespace ZoneEngine_New.Core.GameData
{
    using System.Collections.Generic;

    using AORebirth.Core.GameData;

    using ZoneEngine_New.Core.Mobs;

    /// <summary>
    /// Runtime view of the checked-in GameData tree under {BaseDirectory}\GameData.
    /// </summary>
    public interface IGameData
    {
        string RootPath { get; }

        int MobTemplateCount { get; }

        int LootTableCount { get; }

        int MonsterDataCount { get; }

        bool TryGetMobTemplate(string hash, out MobTemplate template);

        MobTemplate RequireMobTemplate(string hash);

        bool TryGetLootTable(string hash, out IReadOnlyList<LootItemPair> pairs);

        bool TryGetCatMesh(int monsterData, out int catMesh);

        /// <summary>Null when playfield metadata is missing (indoor fallback).</summary>
        PlayfieldMetaData? GetPlayfieldMetaData(int playfieldId);

        /// <summary>Missing Spawns.json yields an empty Spawns array (no throw).</summary>
        PlayfieldSpawnsData GetPlayfieldSpawns(int playfieldId);

        /// <summary>
        /// Walls.dat / Dynels.dat / Collision.dat. Missing files yield null members (no throw).
        /// </summary>
        PlayfieldGeometryData GetPlayfieldGeometry(int playfieldId);
    }
}
