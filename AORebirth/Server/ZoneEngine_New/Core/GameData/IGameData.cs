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

        RespawnContentCatalog RespawnContent => RespawnContentCatalog.Load(RootPath);

        int MobTemplateCount { get; }

        int HashTemplateCount { get; }

        int HashInstanceCount { get; }

        int MonsterDataCount { get; }

        int XpLevelCount { get; }

        bool TryGetXpLevel(int level, out XpLevelEntry entry);

        bool CanResolveMobHash(string hash) => TryGetMobTemplate(hash, out _);

        /// <summary>Like <see cref="CanResolveMobHash"/> but false when only the placeholder fallback would match.</summary>
        bool HasMobTemplate(string hash) => CanResolveMobHash(hash);

        /// <summary>
        /// True when <paramref name="hash"/> has no authored NPC template but is in ItemTemplates.json, so it
        /// spawns as a static dynel instead of the placeholder NPC.
        /// </summary>
        bool IsStaticSpawnHash(string hash)
            => !string.IsNullOrEmpty(hash) && !HasMobTemplate(hash) && CanResolveItemHash(hash);

        bool TryGetMobTemplate(string hash, out MobTemplate template);

        bool TryResolveMobTemplate(string hash, int? level, out MobTemplate template)
            => TryGetMobTemplate(hash, out template);

        MobTemplate RequireMobTemplate(string hash);

        Dictionary<int, int> ComposeNpcStats(MobTemplate template, int? level)
            => new Dictionary<int, int>(template.Stats);

        bool TryGetMonsterWeapon(string hash, out int[] ids)
        { ids = []; return false; }

        bool TryGetHashTemplate(string hash, out IReadOnlyList<string> childHashes);

        bool TryGetHashInstance(string hash, out HashInstance instance);

        /// <summary>Returns the unique assigned leaf hash for one exact LowId/HighId pair.</summary>
        bool TryGetAssignedItemHash(int lowId, int highId, out string hash)
        { hash = string.Empty; return false; }

        bool TryResolveHashInstance(string hash, out HashInstance instance);

        /// <summary>True when some leaf item family is reachable from <paramref name="hash"/>.</summary>
        bool CanResolveItemHash(string hash);

        /// <summary>
        /// Item families one loot roll or world-item spawn should create.
        /// A parent with optional SpawnAll contributes every child branch; other parents contribute one random child.
        /// </summary>
        void CollectHashSpawns(string hash, List<HashInstance> into);

        /// <summary>
        /// NPCs one spawn of <paramref name="hash"/> should create.
        /// A parent with optional SpawnAll contributes every child branch; other parents contribute one random child.
        /// </summary>
        void CollectMobSpawns(string hash, int? level, List<MobTemplate> into);

        /// <summary>Appends every leaf item family reachable from <paramref name="hash"/>.</summary>
        void CollectHashLeafInstances(string hash, List<HashInstance> into);

        /// <summary>Shop stock table for a vending machine template id.</summary>
        bool TryGetVendingMachine(int templateId, out VendingMachineDefinition definition);

        bool TryGetCatMesh(int monsterData, out int catMesh);

        /// <summary>Playfield-local character MonsterData override for SimpleCharFullUpdate.</summary>
        bool TryGetPlayfieldCharacterAppearanceOverride(int playfieldId, out uint monsterData);

        /// <summary>Null when playfield metadata is missing (indoor fallback).</summary>
        PlayfieldMetaData? GetPlayfieldMetaData(int playfieldId);

        /// <summary>Missing Spawns.json yields an empty Spawns array (no throw).</summary>
        PlayfieldSpawnsData GetPlayfieldSpawns(int playfieldId);

        /// <summary>Missing Npcs.json yields an empty playfield NPC catalog (no throw).</summary>
        PlayfieldNpcContentCatalog GetPlayfieldNpcs(int playfieldId)
            => PlayfieldNpcContentCatalog.Load(RootPath, playfieldId);

        /// <summary>
        /// Walls.dat / Dynels.dat / Doors.dat / Collision.dat. Missing files yield null members (no throw).
        /// </summary>
        PlayfieldGeometryData GetPlayfieldGeometry(int playfieldId);

        /// <summary>
        /// Door instances on <paramref name="playfieldId"/> that are TeleportProxy return exits
        /// (static catalog, built once from all playfield Dynels.dat). Empty when none.
        /// </summary>
        IReadOnlyList<int> GetExitProxyDoorInstances(int playfieldId);

        /// <summary>
        /// Explicit destination door allow-list for reverse ExitProxy surfaces. Null means use
        /// portal data without an authored allow-list for this playfield.
        /// </summary>
        IReadOnlyCollection<int>? GetConfiguredExitProxyDoorInstances(int playfieldId);
    }
}

