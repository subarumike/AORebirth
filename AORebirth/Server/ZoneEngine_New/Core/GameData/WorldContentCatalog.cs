namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmokeLounge.AOtomation.Messaging.GameData;

/// <summary>Editable explicit actors in the existing GameData content tree.</summary>
public sealed class WorldContentCatalog
{
    public static WorldContentCatalog Empty { get; } = new();
    public int SchemaVersion { get; set; } = 1;
    public WorldNpcDefinition[] Npcs { get; set; } = [];
    public WorldDestination? Respawn { get; set; }
    public WorldAppearanceOverride[] CharacterAppearanceOverrides { get; set; } = [];
    public WorldExitDoorRule[] ExitDoorRules { get; set; } = [];
    public WorldCorpseDefaults CorpseDefaults { get; set; } = new();
    public static WorldContentCatalog Load(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return Empty;
        string path = Path.Combine(root, "WorldContent.json");
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;
    }
    public static WorldContentCatalog Parse(string json)
    {
        var content = JsonSerializer.Deserialize<WorldContentCatalog>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true })
            ?? throw new InvalidDataException("World content is null.");
        content.Validate();
        return content;
    }
    public void Validate()
    {
        if (SchemaVersion != 1 || Npcs == null
            || CharacterAppearanceOverrides == null || ExitDoorRules == null || CorpseDefaults == null)
            throw new InvalidDataException("Unsupported or incomplete world content document.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (Respawn != null && (Respawn.PlayfieldId <= 0 || Respawn.Position is not { Length: 3 }
            || Respawn.Position.Any(v => !float.IsFinite(v)))) throw new InvalidDataException("Invalid respawn destination.");
        if (CharacterAppearanceOverrides.Any(p => p.PlayfieldId <= 0 || p.MonsterData <= 0)
            || CharacterAppearanceOverrides.Select(p => p.PlayfieldId).Distinct().Count() != CharacterAppearanceOverrides.Length
            || ExitDoorRules.Any(p => p.PlayfieldId <= 0 || p.AllowedDoorInstances == null)
            || ExitDoorRules.Select(p => p.PlayfieldId).Distinct().Count() != ExitDoorRules.Length
            || CorpseDefaults.AnimationEffects == null || CorpseDefaults.MonsterDataAliases == null
            || CorpseDefaults.MonsterDataAliases.Any(p => p.Key <= 0 || p.Value <= 0))
            throw new InvalidDataException("Invalid or duplicate world presentation/door rules.");
        var identities = new HashSet<(int, int)>();
        var shopIdentities = new HashSet<(int, int)>();
        foreach (var npc in Npcs)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.Key) || !keys.Add(npc.Key)
                || npc.PlayfieldId < 0 || npc.InstanceId <= 0 || string.IsNullOrWhiteSpace(npc.Name)
                || (npc.PlayfieldId > 0 && !identities.Add((npc.PlayfieldId, npc.InstanceId)))
                || npc.Stats == null || npc.Textures == null || npc.Meshes == null || npc.Presentation == null
                || (npc.HasDialogue && string.IsNullOrWhiteSpace(npc.ContentNpcIdentity)))
                throw new InvalidDataException("Invalid or duplicate NPC definition.");
            RequireTransform(npc.Position, npc.Rotation, npc.Key);
            if (npc.Textures.Any(t => t == null || t.Place < 0 || t.Id < 0)
                || npc.Meshes.Any(m => m == null))
                throw new InvalidDataException("Invalid NPC appearance: " + npc.Key);
            if (npc.Vendor != null)
            {
                RequireVendor(npc.Vendor);
                if (npc.PlayfieldId > 0 && !shopIdentities.Add((npc.PlayfieldId, npc.Vendor.InstanceId)))
                    throw new InvalidDataException("Duplicate world shop identity.");
            }
        }
    }
    internal static void RequireTransform(float[] position, float[] rotation, string key)
    {
        if (position is not { Length: 3 } || position.Any(v => !float.IsFinite(v))
            || rotation is not { Length: 4 } || rotation.Any(v => !float.IsFinite(v))
            || rotation.All(v => v == 0)) throw new InvalidDataException("Invalid world transform: " + key);
    }
    internal static void RequireVendor(WorldVendorDefinition vendor)
    {
        if (vendor == null || vendor.InstanceId <= 0 || vendor.TemplateId <= 0 || vendor.Stock == null
            || vendor.Stock.Length == 0 || vendor.Stock.Where((row, index) => row == null || row.Slot != index
                || row.LowId <= 0 || row.HighId <= 0 || row.Quality <= 0).Any())
            throw new InvalidDataException("Invalid vendor identity, template or stock.");
    }
}

public sealed class WorldDestination { public int PlayfieldId { get; set; } public float[] Position { get; set; } = []; }
public sealed class WorldAppearanceOverride { public int PlayfieldId { get; set; } public uint MonsterData { get; set; } }
public sealed class WorldExitDoorRule { public int PlayfieldId { get; set; } public int[] AllowedDoorInstances { get; set; } = []; }
public sealed class WorldCorpseDefaults
{
    public int Flags { get; set; }
    public AnimationEffect[] AnimationEffects { get; set; } = [];
    public Dictionary<int, int> MonsterDataAliases { get; set; } = new();
}

public sealed class WorldNpcDefinition
{
    public string Key { get; set; } = string.Empty;
    /// <summary>Informational provenance; never used to grant runtime permission.</summary>
    public string Provenance { get; set; } = string.Empty;
    public string ContentNpcIdentity { get; set; } = string.Empty;
    /// <summary>Zero denotes a template instantiated by a mechanic rather than world activation.</summary>
    public int PlayfieldId { get; set; }
    public int InstanceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public float[] Position { get; set; } = [];
    public float[] Rotation { get; set; } = [];
    public Dictionary<int, int> Stats { get; set; } = new();
    public WorldTexture[] Textures { get; set; } = [];
    public Mesh[] Meshes { get; set; } = [];
    public bool HasDialogue { get; set; }
    public bool Passive { get; set; }
    public bool PassiveRegen { get; set; } = true;
    public bool Attackable { get; set; }
    public bool TickLiving { get; set; } = true;
    public WorldNpcPresentation Presentation { get; set; } = new();
    public WorldVendorDefinition? Vendor { get; set; }
}
public sealed class WorldTexture { public int Place { get; set; } public int Id { get; set; } }
public sealed class WorldNpcPresentation
{
    public uint? AppearanceValue { get; set; }
    public short? Family { get; set; }
    public short? LosHeight { get; set; }
    public uint? Flags { get; set; }
    public int? Flags2 { get; set; }
    public byte[]? Unknown1 { get; set; }
    public byte? Unknown2 { get; set; }
    public byte? VisibleTitle { get; set; }
    public Texture[]? Textures { get; set; }
    public Mesh[]? Meshes { get; set; }
    public Vector3[]? Waypoints { get; set; }
}
public sealed class WorldVendorDefinition
{
    public int InstanceId { get; set; }
    public int TemplateId { get; set; }
    public WorldVendorStock[] Stock { get; set; } = [];
    public JsonElement? CompanionPacket { get; set; }
}
public sealed class WorldVendorStock { public int Slot { get; set; } public int LowId { get; set; } public int HighId { get; set; } public int Quality { get; set; } }
