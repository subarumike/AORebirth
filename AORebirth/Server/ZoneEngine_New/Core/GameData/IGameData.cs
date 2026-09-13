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

        int NpcFamilyStatTemplateCount { get; }

        int NpcStatTemplateCount { get; }

        int HashTemplateCount { get; }

        int HashInstanceCount { get; }

        int MonsterDataCount { get; }

        int XpLevelCount { get; }

        bool TryGetXpLevel(int level, out XpLevelEntry entry);

        bool TryGetMobTemplate(string hash, out MobTemplate template);

        MobTemplate RequireMobTemplate(string hash);

        /// <summary>Level-scaled family curves. False when the id is unknown or negative.</summary>
        bool TryGetNpcFamilyStatTemplate(int family, out NpcFamilyStatTemplate template);

        /// <summary>
        /// Looks up <paramref name="family"/>; if missing, falls back to
        /// <see cref="MobTemplate.DefaultNpcFamilyId"/>.
        /// </summary>
        bool TryResolveNpcFamilyStatTemplate(int family, out NpcFamilyStatTemplate template);

        /// <summary>Optional overlay curves on top of the family. False when unknown or zero.</summary>
        bool TryGetNpcStatTemplate(int id, out NpcStatTemplate template);

        bool TryGetHashTemplate(string hash, out IReadOnlyList<string> childHashes);

        bool TryGetHashInstance(string hash, out HashInstance instance);

        bool TryResolveHashInstance(string hash, out HashInstance instance);

        /// <summary>Appends every leaf item family reachable from <paramref name="hash"/>.</summary>
        void CollectHashLeafInstances(string hash, List<HashInstance> into);

        /// <summary>Shop stock table for a vending machine template id.</summary>
        bool TryGetVendingMachine(int templateId, out VendingMachineDefinition definition);

        bool TryGetCatMesh(int monsterData, out int catMesh);

        /// <summary>Null when playfield metadata is missing (indoor fallback).</summary>
        PlayfieldMetaData? GetPlayfieldMetaData(int playfieldId);

        /// <summary>Missing Spawns.json yields an empty Spawns array (no throw).</summary>
        PlayfieldSpawnsData GetPlayfieldSpawns(int playfieldId);

        /// <summary>
        /// Walls.dat / Dynels.dat / Doors.dat / Collision.dat. Missing files yield null members (no throw).
        /// </summary>
        PlayfieldGeometryData GetPlayfieldGeometry(int playfieldId);

        /// <summary>
        /// Door instances on <paramref name="playfieldId"/> that are TeleportProxy return exits
        /// (static catalog, built once from all playfield Dynels.dat). Empty when none.
        /// </summary>
        IReadOnlyList<int> GetExitProxyDoorInstances(int playfieldId);
    }
}
