namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmokeLounge.AOtomation.Messaging.GameData;

/// <summary>Generated mission NPC content, using the same editable GameData root as world templates.</summary>
public sealed class MissionNpcContent
{
    public Dictionary<CharacterStat, int> Stats { get; set; } = new();
    public double AggroRadius { get; set; }
    public int WeaponMeshLayer { get; set; }
    public MissionAttackContent Melee { get; set; } = new();
    public MissionAttackContent Ranged { get; set; } = new();
    public MissionWeaponContent[] Weapons { get; set; } = [];
    public string[] Provenance { get; set; } = [];

    public static MissionNpcContent Load(string? gameDataRoot = null)
    {
        var path = Path.Combine(gameDataRoot ?? Path.Combine(AppContext.BaseDirectory, "GameData"), "Missions", "NpcContent.json");
        return Parse(File.ReadAllText(path));
    }

    public static MissionNpcContent Parse(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip };
        options.Converters.Add(new JsonStringEnumConverter());
        var content = JsonSerializer.Deserialize<MissionNpcContent>(json, options)
            ?? throw new InvalidDataException("Mission NPC content is empty.");
        if (content.Stats.Count == 0 || !double.IsFinite(content.AggroRadius) || content.AggroRadius < 0
            || content.WeaponMeshLayer < 0 || content.Weapons.Any(w => w.LowId <= 0 || w.HighId <= 0 || w.MaximumQuality <= 0
                || w.Stats.Count == 0) || content.Weapons.Select(w => (w.LowId, w.HighId)).Distinct().Count() != content.Weapons.Length)
            throw new InvalidDataException("Invalid mission NPC stats or weapon pool.");
        content.Melee.Validate(); content.Ranged.Validate();
        if (content.Ranged.WeaponInstance != 0 || content.Ranged.Slot <= 0 || content.Melee.SpecialLowId <= 0
            || content.Melee.SpecialHighId <= 0 || string.IsNullOrWhiteSpace(content.Melee.SpecialName))
            throw new InvalidDataException("Unsupported mission attack ownership or missing special-attack templates.");
        return content;
    }
}

public sealed class MissionAttackContent
{
    public string Id { get; set; } = string.Empty;
    public int Slot { get; set; }
    public int WeaponInstance { get; set; }
    public int Ammo { get; set; }
    public int HitType { get; set; }
    public int Initiative { get; set; }
    public int SpecialWeaponUnknown { get; set; }
    public double Range { get; set; }
    public double StartDelay { get; set; }
    public double FirstHitDelay { get; set; }
    public double Interval { get; set; }
    public int SpecialLowId { get; set; }
    public int SpecialHighId { get; set; }
    public string SpecialName { get; set; } = string.Empty;
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || !double.IsFinite(Range) || Range <= 0 || !double.IsFinite(StartDelay)
            || StartDelay < 0 || !double.IsFinite(FirstHitDelay) || FirstHitDelay < 0 || !double.IsFinite(Interval)
            || Interval <= 0 || SpecialLowId < 0 || SpecialHighId < 0 || Slot < 0)
            throw new InvalidDataException("Invalid mission NPC attack definition.");
    }
}

public sealed class MissionWeaponContent
{
    public int LowId { get; set; }
    public int HighId { get; set; }
    public int MaximumQuality { get; set; }
    public Dictionary<CharacterStat, uint> Stats { get; set; } = new();
}
