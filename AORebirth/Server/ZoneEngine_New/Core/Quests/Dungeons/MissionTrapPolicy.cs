namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;

/// <summary>Editable AORebirth selection policy. No retail probability or skill formula is implied.</summary>
public sealed class MissionTrapPolicy
{
    public bool Enabled { get; set; }
    public double ChestChance { get; set; }
    public int MaximumCount { get; set; }
    public MissionTrapProfile[] Profiles { get; set; } = [];
    public MissionTrapInteractionPolicy Interaction { get; set; } = new();

    [JsonIgnore]
    public bool IsValid => double.IsFinite(ChestChance) && ChestChance is >= 0 and <= 1
        && MaximumCount is >= 0 and <= 128 && Profiles is { Length: <= 128 } && Interaction != null
        && (!Enabled || (MaximumCount > 0 && Profiles.Length > 0 && Interaction.IsValid))
        && Profiles.All(profile => profile != null && profile.IsValid)
        && Profiles.Select(profile => profile.Id).Distinct(StringComparer.Ordinal).Count() == Profiles.Length;

    public MissionTrapPolicy Clone() => JsonSerializer.Deserialize<MissionTrapPolicy>(JsonSerializer.Serialize(this))!;

    internal MissionTrapProfile? Select(int quality, Random random)
    {
        MissionTrapProfile[] eligible = Profiles.Where(profile => quality >= profile.MinMissionQuality
            && quality <= profile.MaxMissionQuality).ToArray();
        double sum = eligible.Sum(profile => profile.Weight);
        if (!double.IsFinite(sum) || sum <= 0)
            return null;
        double roll = random.NextDouble() * sum;
        foreach (MissionTrapProfile profile in eligible)
        {
            roll -= profile.Weight;
            if (roll < 0)
                return profile;
        }
        return eligible[^1];
    }
}

/// <summary>Reviewed resource/effect association supplied by private editable content.</summary>
public sealed class MissionTrapProfile
{
    public string Id { get; set; } = string.Empty;
    public int MinMissionQuality { get; set; }
    public int MaxMissionQuality { get; set; }
    public double Weight { get; set; }
    public int NanoId { get; set; }
    public MissionTrapSpawnDefinition Spawn { get; set; } = new();

    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Id.Length <= 64
        && MinMissionQuality >= 1 && MaxMissionQuality <= 250 && MinMissionQuality <= MaxMissionQuality
        && double.IsFinite(Weight) && Weight > 0 && NanoId > 0 && Spawn?.IsValid == true;
}

/// <summary>Recovered parented TrapItem FullUpdate fields; neutral native fields keep their offset names.</summary>
public sealed class MissionTrapSpawnDefinition
{
    public byte PassOn { get; set; }
    public int CommonMarker { get; set; } = 11;
    public int NeutralType { get; set; }
    public int NeutralInstance { get; set; }
    public short HeaderStatBytes { get; set; }
    public MissionTrapStat[] Stats { get; set; } = [];
    public byte[] OpaqueData { get; set; } = [];
    public int TrapMarker { get; set; } = 2;
    public int Raw74 { get; set; }
    public int Identity78Type { get; set; }
    public int Identity78Instance { get; set; }
    public int Identity80Type { get; set; }
    public int Identity80Instance { get; set; }
    public byte TrapFlags { get; set; }

    [JsonIgnore]
    public bool IsValid => PassOn <= 1 && CommonMarker == 11 && TrapMarker == 2 && Stats is { Length: > 0 and <= 128 }
        && Stats.All(stat => stat != null && stat.StatId >= 0)
        && Stats.Select(stat => stat.StatId).Distinct().Count() == Stats.Length
        && OpaqueData != null && OpaqueData.Length <= 1024;

    internal TrapItemFullUpdateMessage Create(Identity identity, Identity parent, int playfield)
        => new()
        {
            Identity = identity, Unknown = PassOn, Parent = parent, PlayfieldId = playfield,
            CommonMarker = CommonMarker,
            NeutralIdentity = new Identity { Type = (IdentityType)NeutralType, Instance = NeutralInstance },
            HeaderStatBytes = HeaderStatBytes,
            Stats = Stats.Select(stat => new GameTuple<CharacterStat, uint>
                { Value1 = (CharacterStat)stat.StatId, Value2 = stat.Value }).ToArray(),
            OpaqueData = (byte[])OpaqueData.Clone(), TrapMarker = TrapMarker, Raw74 = Raw74,
            Identity78 = new Identity { Type = (IdentityType)Identity78Type, Instance = Identity78Instance },
            Identity80 = new Identity { Type = (IdentityType)Identity80Type, Instance = Identity80Instance },
            TrapFlags = TrapFlags
        };
}

public sealed class MissionTrapStat
{
    public int StatId { get; set; }
    public uint Value { get; set; }
}
