namespace ZoneEngine_New.Core.Inventory;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmokeLounge.AOtomation.Messaging.GameData;

/// <summary>Editable item mechanic bindings. Identity/loot/curve values are content;
/// inventory ownership, transactions and packet semantics remain runtime services.</summary>
public sealed class ItemBehaviorContent
{
    public static ItemBehaviorContent Current { get; } = Load();
    public int[] ProtectedItems { get; set; } = [];
    public ItemPackageContent[] Packages { get; set; } = [];
    public VitalItemContent[] VitalItems { get; set; } = [];
    public FistWeaponContent[] FistWeapons { get; set; } = [];
    public WornMeshOverrideContent[] WornMeshOverrides { get; set; } = [];
    public string[] Provenance { get; set; } = [];

    public static ItemBehaviorContent Load(string? gameDataRoot = null)
        => Parse(File.ReadAllText(Path.Combine(gameDataRoot ?? Path.Combine(AppContext.BaseDirectory, "GameData"), "ItemBehavior.json")));

    public static ItemBehaviorContent Parse(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var result = JsonSerializer.Deserialize<ItemBehaviorContent>(json, options)
            ?? throw new InvalidDataException("Item behavior content is empty.");
        if (result.ProtectedItems.Any(id => id <= 0) || result.Packages.Any(p => p.SourceIds.Length == 0
            || p.SourceIds.Any(id => id <= 0) || p.ProductLowId <= 0 || p.ProductHighId <= 0 || p.Quality <= 0)
            || result.VitalItems.Any(v => v.TemplateIds.Length == 0 || v.TemplateIds.Any(id => id <= 0)
                || !Enum.IsDefined(v.LockStat) || v.LockSeconds <= 0 || v.MinimumQuality < 1 || v.MaximumQuality < v.MinimumQuality
                || v.MinimumRestore < 0 || v.MaximumRestore < v.MinimumRestore)
            || result.Packages.SelectMany(p => p.SourceIds).Distinct().Count() != result.Packages.Sum(p => p.SourceIds.Length)
            || result.VitalItems.SelectMany(v => v.TemplateIds).Distinct().Count() != result.VitalItems.Sum(v => v.TemplateIds.Length))
            throw new InvalidDataException("Invalid or ambiguous item behavior content.");
        if (result.FistWeapons.Any(value => value.Tier <= 0 || value.LowId <= 0 || value.HighId <= 0
                || value.Profession.HasValue && !Enum.IsDefined(value.Profession.Value))
            || result.FistWeapons.Select(value => (value.Profession, value.Tier)).Distinct().Count() != result.FistWeapons.Length)
            throw new InvalidDataException("Invalid or ambiguous unarmed weapon assignment.");
        if (result.WornMeshOverrides.Any(value => value.TemplateIds.Length == 0
                || value.TemplateIds.Any(id => id <= 0)
                || value.MeshId <= 0
                || value.OverrideTextureId <= 0)
            || result.WornMeshOverrides.SelectMany(value => value.TemplateIds.Select(id => (id, value.MeshId)))
                .Distinct().Count() != result.WornMeshOverrides.Sum(value => value.TemplateIds.Length))
            throw new InvalidDataException("Invalid or ambiguous worn mesh override content.");
        return result;
    }

    public ItemPackageContent? FindPackage(Item item) => Packages.SingleOrDefault(p => p.SourceIds.Contains(item.LowId) || p.SourceIds.Contains(item.HighId));
    public VitalItemContent? FindVitalItem(Item item) => VitalItems.SingleOrDefault(v => v.TemplateIds.Contains(item.LowId) || v.TemplateIds.Contains(item.HighId));
    public bool IsProtected(Item item) => ProtectedItems.Contains(item.LowId) || ProtectedItems.Contains(item.HighId);
    public bool TryResolveWornMeshOverride(Item item, int meshId, int overrideTextureId, out int resolvedOverrideTextureId)
    {
        resolvedOverrideTextureId = overrideTextureId;
        WornMeshOverrideContent? found = WornMeshOverrides.SingleOrDefault(
            value => value.MeshId == meshId
                && value.OnlyWhenOverrideTextureId == overrideTextureId
                && (value.TemplateIds.Contains(item.LowId) || value.TemplateIds.Contains(item.HighId)));
        if (found == null)
            return false;

        resolvedOverrideTextureId = found.OverrideTextureId;
        return true;
    }
}

public sealed class FistWeaponContent
{
    public Profession? Profession { get; set; }
    public int Tier { get; set; }
    public int LowId { get; set; }
    public int HighId { get; set; }
}

public sealed class ItemPackageContent
{
    public int[] SourceIds { get; set; } = [];
    public int ProductLowId { get; set; }
    public int ProductHighId { get; set; }
    public int Quality { get; set; }
    public bool Unique { get; set; }
}

public sealed class VitalItemContent
{
    public int[] TemplateIds { get; set; } = [];
    public bool Consumed { get; set; }
    public CharacterStat LockStat { get; set; }
    public int LockSeconds { get; set; }
    public int MinimumQuality { get; set; }
    public int MaximumQuality { get; set; }
    public int MinimumRestore { get; set; }
    public int MaximumRestore { get; set; }
    public int RestoreAt(int quality)
    {
        quality = Math.Clamp(quality, MinimumQuality, MaximumQuality);
        return MaximumQuality == MinimumQuality ? MinimumRestore
            : checked((int)(MinimumRestore + (long)(MaximumRestore - MinimumRestore) * (quality - MinimumQuality) / (MaximumQuality - MinimumQuality)));
    }
}

public sealed class WornMeshOverrideContent
{
    public int[] TemplateIds { get; set; } = [];
    public int MeshId { get; set; }
    public int OnlyWhenOverrideTextureId { get; set; }
    public int OverrideTextureId { get; set; }
}
