namespace ZoneEngine.Core.Missions;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AORebirth.Interfaces.Persistence.Missions;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

// Permanent, decoded content. Copies are JSON object copies, never historical
// packet replay. The existing AOtomation serializers own outgoing wire bytes.
internal static class MissionTypedJson
{
    internal static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
    internal static T Copy<T>(T value) => JsonSerializer.SerializeToElement(value, Options).Deserialize<T>(Options)
        ?? throw new InvalidDataException("Missing typed mission content.");
}

internal sealed class MissionOfferContent
{
    [JsonRequired] public int FormatVersion { get; set; }
    [JsonRequired] public QuestAlternativeMessage[] Responses { get; set; } = [];
    internal static MissionOfferContent Current { get; } = Load();
    static MissionOfferContent Load()
    {
        var result = JsonSerializer.Deserialize<MissionOfferContent>(File.ReadAllText(Path.Combine(MissionContentJson.RootPath, "MissionOffers.json")), MissionTypedJson.Options)
            ?? throw new InvalidDataException("Missing typed mission offers.");
        if (result.FormatVersion != 1 || result.Responses.Length == 0 || result.Responses.Any(value => value?.QuestInfos is not { Length: > 0 }))
            throw new InvalidDataException("Invalid typed mission offers.");
        return result;
    }
    internal QuestAlternativeMessage CopyResponse(int index) => MissionTypedJson.Copy(Responses[index]);
}

public sealed class MissionNpcAppearance
{
    [JsonRequired] public short Scale { get; set; }
    [JsonRequired] public uint? HeadMesh { get; set; }
    [JsonRequired] public Texture[] Textures { get; set; } = [];
    [JsonRequired] public Mesh[] Meshes { get; set; } = [];
    internal void Validate()
    {
        if (Scale <= 0 || Textures == null || Meshes == null)
            throw new InvalidDataException("Invalid mission NPC appearance.");
    }
}

internal sealed class MissionSpawnContent
{
    public DoorFullUpdateMessage? Door { get; set; }
    public ChestItemFullUpdateMessage? Chest { get; set; }
    public SimpleItemFullUpdateMessage? Item { get; set; }
    public WeaponItemFullUpdateMessage? Weapon { get; set; }
    internal void Validate()
    {
        if (new object?[] { Door, Chest, Item, Weapon }.Count(value => value != null) != 1)
            throw new InvalidDataException("A mission object requires exactly one typed spawn definition.");
        // The normalized mission corpus consists of ownerless world objects.
        // Introducing an owned object requires an explicit typed ownership policy.
        if (Door is { } d && (d.Owner != Identity.None || d.Stats == null || d.Identities == null)
            || Chest is { } c && (c.Owner != Identity.None || c.Stats == null || c.UnknownArray == null)
            || Item is { } i && (i.Owner != Identity.None || i.Stats == null)
            || Weapon is { } w && (w.Owner != Identity.None || w.Stats == null))
            throw new InvalidDataException("Invalid mission world object content.");
    }
    internal void ValidatePlacement(MissionAcgIdentityRecord identity, int? playfield)
    {
        Validate();
        N3Message body = (N3Message?)Door ?? (N3Message?)Chest ?? (N3Message?)Item ?? Weapon!;
        int sourcePlayfield = Door?.Playfield ?? Chest?.PlayfieldId ?? Item?.Playfield ?? Weapon!.PlayfieldId;
        if (identity == null || (int)body.Identity.Type != identity.Type || body.Identity.Instance != identity.Instance
            || playfield != sourcePlayfield)
            throw new InvalidDataException("Typed mission definition conflicts with its permanent placement identity/playfield.");
    }
    internal MessageBody Create(GeneratedMissionBinding binding, MissionAcgIdentityRecord identity)
    {
        Validate();
        var copy = MissionTypedJson.Copy(this);
        N3Message message;
        if (copy.Door is { } door) { door.Playfield = binding.LivePlayfield; message = door; }
        else if (copy.Chest is { } chest) { chest.PlayfieldId = binding.LivePlayfield; message = chest; }
        else if (copy.Item is { } item) { item.Playfield = binding.LivePlayfield; message = item; }
        else if (copy.Weapon is { } weapon) { weapon.PlayfieldId = binding.LivePlayfield; message = weapon; }
        else throw new InvalidDataException("Missing mission world object content.");
        message.Identity = new Identity { Type = (IdentityType)identity.Type, Instance = identity.Instance };
        return message;
    }
}
