namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AORebirth.Enums;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory;

/// <summary>The validated item roles and grant policy for generated mission keys.</summary>
public sealed class MissionKeyPolicy
{
    public const string RelativePath = "Missions/Keys.json";

    public int KeyTemplateId { get; }
    public int DuplicatorTemplateId { get; }
    public int KeyQuality { get; }
    public int MaxKeysPerQuest { get; }
    public string NamePrefix { get; }

    MissionKeyPolicy(PolicyData data)
    {
        KeyTemplateId = data.KeyTemplateId;
        DuplicatorTemplateId = data.DuplicatorTemplateId;
        KeyQuality = data.KeyQuality;
        MaxKeysPerQuest = data.MaxKeysPerQuest;
        NamePrefix = data.NamePrefix!;
    }

    public static MissionKeyPolicy Load(string gameDataRoot, IItemTemplateCatalog items)
    {
        ArgumentNullException.ThrowIfNull(items);
        string path = Path.Combine(gameDataRoot, RelativePath);
        if (!File.Exists(path))
            throw new InvalidDataException("Required mission key policy is missing: " + path);

        PolicyData? data;
        try
        {
            data = JsonSerializer.Deserialize<PolicyData>(File.ReadAllText(path), new JsonSerializerOptions
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Invalid mission key policy: " + path, exception);
        }

        if (data == null || data.Version != 1 || data.KeyTemplateId <= 0 || data.DuplicatorTemplateId <= 0
            || data.KeyTemplateId == data.DuplicatorTemplateId || data.KeyQuality <= 0 || data.MaxKeysPerQuest <= 0
            || string.IsNullOrWhiteSpace(data.NamePrefix) || data.NamePrefix.Contains('\0'))
            throw new InvalidDataException("Invalid version or values in mission key policy: " + path);

        ValidateRole(items, data.KeyTemplateId, IdentityType.MissionKey, data.KeyQuality);
        ValidateRole(items, data.DuplicatorTemplateId, IdentityType.MissionKeyDuplicator, null);
        return new MissionKeyPolicy(data);
    }

    static void ValidateRole(IItemTemplateCatalog items, int templateId, IdentityType role, int? quality)
    {
        const CanFlags required = CanFlags.Carry | CanFlags.Use;
        if (!items.TryGet(templateId, out ItemTemplate definition) || definition.DynelType != (int)role
            || definition.Quality <= 0 || (quality.HasValue && definition.Quality != quality.Value)
            || !definition.Stats.TryGetValue(CharacterStat.Can, out int can) || ((CanFlags)can & required) != required)
            throw new InvalidDataException($"{RelativePath} template {templateId} cannot serve as {role} at its configured quality.");
    }

    sealed class PolicyData
    {
        public int Version { get; init; }
        public int KeyTemplateId { get; init; }
        public int DuplicatorTemplateId { get; init; }
        public int KeyQuality { get; init; }
        public int MaxKeysPerQuest { get; init; }
        public string? NamePrefix { get; init; }
    }
}
