namespace ZoneEngine_New.Core.Missions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AORebirth.Core.GameData;

/// <summary>
/// Quest props and static uses, from GameData/PlayfieldContent/&lt;playfield&gt;/Props.json: objects a Quests.json step
/// is done on (open the Cargo Box, pick the Merchant's Strongbox, use the Surgery Clinic).
/// </summary>
public sealed class QuestPropContent
{
    static readonly Lazy<QuestPropContent> CurrentContent = new(() => Load());
    public static QuestPropContent Current => CurrentContent.Value;

    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public QuestPropDefinition[] Props { get; set; } = [];
    public StaticUseDefinition[] StaticUses { get; set; } = [];

    public static QuestPropContent Load(string? gameDataRoot = null)
    {
        string folder = Path.Combine(gameDataRoot ?? GameDataPaths.ResolveRuntimeRoot(), "PlayfieldContent");
        var props = new List<QuestPropDefinition>();
        var uses = new List<StaticUseDefinition>();
        if (Directory.Exists(folder))
        {
            foreach (string path in Directory.EnumerateFiles(folder, "Props.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var file = JsonSerializer.Deserialize<QuestPropContent>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException("Empty quest prop content: " + path);
                props.AddRange(file.Props);
                uses.AddRange(file.StaticUses);
            }
        }

        var content = new QuestPropContent { Props = props.ToArray(), StaticUses = uses.ToArray() };
        content.Validate();
        return content;
    }

    public void Validate()
    {
        if (Props.GroupBy(x => (x.Playfield, x.Instance)).Any(x => x.Count() > 1)
            // No CompleteQuests on a plain-use prop: scenery. An item target always names its quests and items.
            || Props.Any(x => x.ItemTarget && (x.CompleteQuests.Length == 0 || x.UseItemIds.Length == 0)
                || x.UseItemIds.Any(id => id <= 0) || x.RespawnSeconds < 0
                || x.CompleteQuests.Any(string.IsNullOrWhiteSpace) || x.GuardNpcs.Any(string.IsNullOrWhiteSpace)
                || x.Position.Length != 3 || x.Rotation.Length != 4
                || x.SpawnOnUse is { } spawn && (string.IsNullOrWhiteSpace(spawn.Hash) || spawn.Position.Length != 3
                    || spawn.DespawnSeconds <= 0 || spawn.Path.Any(p => p == null || p.Length != 3))
                || x.Position.Concat(x.Rotation).Any(x => !float.IsFinite(x)))) throw new InvalidDataException("Invalid quest prop content.");
        if (StaticUses.GroupBy(x => (x.Playfield, x.Instance)).Any(x => x.Count() > 1)
            || StaticUses.Any(x => x.Instance == 0 || x.CompleteQuests.Length == 0 || x.CompleteQuests.Any(string.IsNullOrWhiteSpace)
                || x.Position.Length != 3 || x.Position.Any(p => !float.IsFinite(p)))) throw new InvalidDataException("Invalid static use content.");
    }
}

public sealed class QuestPropDefinition
{
    public int Instance { get; set; } public int Playfield { get; set; } public int TemplateId { get; set; }
    public float[] Position { get; set; } = []; public float[] Rotation { get; set; } = [];
    public bool ItemTarget { get; set; }
    public Dictionary<int,uint> Stats { get; set; } = new();
    /// <summary>Quests.json quests a use of this prop completes when active ("Open the Cargo Box").</summary>
    public string[] CompleteQuests { get; set; } = [];
    /// <summary>Spawn hash of the statel this prop stands in for: that hash's point here spawns nothing itself.</summary>
    public string ReplacesHash { get; set; } = "";
    /// <summary>
    /// Spawn hashes of NPCs guarding this prop (the Robotic Guard Dog by the Merchant's Strongbox): they never attack on
    /// sight, but go for a player who uses an item on the prop if they would notice them.
    /// </summary>
    public string[] GuardNpcs { get; set; } = [];
    /// <summary>A creature that comes out when a use counts (the Cleaning Robot from the Arete Cargo Box).</summary>
    public PropSpawnDefinition? SpawnOnUse { get; set; }
    /// <summary>With ItemTarget: the item templates whose use on the prop counts (kept unless ConsumeUsedItem).</summary>
    public int[] UseItemIds { get; set; } = [];
    /// <summary>The used item stays on the prop and leaves the inventory (Plant a Bug: the RC-P Audio Recording Device).</summary>
    public bool ConsumeUsedItem { get; set; }
    /// <summary>After a use that counts the prop is gone for this long, then returns (a put-out Gas Fire). 0 stays.</summary>
    public int RespawnSeconds { get; set; }
    /// <summary>Formatted feedback shown on a use that counts ("You successfully picked this lock and obtained ...").</summary>
    public string Feedback { get; set; } = "";
}

/// <summary>
/// A use of an object the client draws from its own playfield data (the Arete Surgery Clinic, the ICC Cell Structure
/// Scanner): no prop is spawned for it, the use still does what it does, and it also completes these Quests.json steps
/// when active ("Install the implant").
/// </summary>
public sealed class StaticUseDefinition
{
    public int Playfield { get; set; } public int Instance { get; set; } public int TemplateId { get; set; }
    public float[] Position { get; set; } = [];
    public string[] CompleteQuests { get; set; } = [];
}

/// <summary>An NPC spawned by a prop's use: it appears at Position, walks Path and is removed after DespawnSeconds.</summary>
public sealed class PropSpawnDefinition
{
    public string Hash { get; set; } = "";
    public float[] Position { get; set; } = [];
    public float[][] Path { get; set; } = [];
    public int DespawnSeconds { get; set; } = 60;
}
