namespace ZoneEngine_New.Core.GameData;

using System.Collections.Generic;
using System.Text.Json;

using SmokeLounge.AOtomation.Messaging.GameData;

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
    public bool Passive { get; set; }
    public bool PassiveRegen { get; set; } = true;
    public bool Attackable { get; set; }
    public bool TickLiving { get; set; } = true;
    public WorldNpcPresentation Presentation { get; set; } = new();
    public WorldVendorDefinition? Vendor { get; set; }
    /// <summary>Spawn hash this placed NPC stands in for: that hash's points on the playfield spawn nothing.</summary>
    public string ReplacesHash { get; set; } = string.Empty;
    /// <summary>Weapon items [low, high] it fights with, minted at its level (needs Passive false).</summary>
    public List<List<int>> CombatWeapons { get; set; } = new();
    /// <summary>NPCs it fights on its own (Marcus Stone and the Burning Cleaning Robots).</summary>
    public ZoneEngine_New.Core.Mobs.NpcDefenseDefinition? Defense { get; set; }
}

public sealed class WorldStandaloneShopDefinition
{
    public string Key { get; set; } = string.Empty;
    /// <summary>Informational provenance; never used to grant runtime permission.</summary>
    public string Provenance { get; set; } = string.Empty;
    public int PlayfieldId { get; set; }
    public float[] Position { get; set; } = [];
    public float[] Rotation { get; set; } = [];
    public WorldVendorDefinition Vendor { get; set; } = new();
}

public sealed class WorldTexture
{
    public int Place { get; set; }
    public int Id { get; set; }
}

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

public sealed class WorldVendorStock
{
    public int Slot { get; set; }
    public int LowId { get; set; }
    public int HighId { get; set; }
    public int Quality { get; set; }
}
