using System;
using System.Runtime.Serialization;

namespace AORebirth.NpcEditor.Official;

/// <summary>Versioned local bridge contract shared with the installed .NET Framework AOSharp plugin.</summary>
[DataContract]
public sealed class OfficialSnapshot
{
    public const int CurrentVersion = 2;

    [DataMember(IsRequired = true)] public int Version { get; set; } = CurrentVersion;
    [DataMember(IsRequired = true)] public int ProcessId { get; set; }
    [DataMember(IsRequired = true)] public string SessionId { get; set; } = string.Empty;
    [DataMember(IsRequired = true)] public long CapturedUtcTicks { get; set; }
    [DataMember(IsRequired = true)] public long Sequence { get; set; }
    [DataMember(IsRequired = true)] public string State { get; set; } = "unavailable";
    [DataMember(EmitDefaultValue = false)] public string? SelectionId { get; set; }
    [DataMember(IsRequired = true)] public int PlayfieldId { get; set; }
    [DataMember(IsRequired = true)] public int IdentityType { get; set; }
    [DataMember(IsRequired = true)] public int InstanceId { get; set; }
    [DataMember(EmitDefaultValue = false)] public string? Name { get; set; }
    [DataMember(EmitDefaultValue = false)] public int? MonsterData { get; set; }
    [DataMember(EmitDefaultValue = false)] public int? Level { get; set; }
    [DataMember(IsRequired = true)] public OfficialField[] Fields { get; set; } = Array.Empty<OfficialField>();
    [DataMember(IsRequired = true)] public OfficialEvent[] Events { get; set; } = Array.Empty<OfficialEvent>();
}

[DataContract]
public sealed class OfficialField
{
    [DataMember(IsRequired = true)] public string Key { get; set; } = string.Empty;
    [DataMember(EmitDefaultValue = false)] public string? Value { get; set; }
    [DataMember(EmitDefaultValue = false)] public string? Unit { get; set; }
    [DataMember(IsRequired = true)] public string Source { get; set; } = string.Empty;
    [DataMember(IsRequired = true)] public string Classification { get; set; } = string.Empty;
    [DataMember(EmitDefaultValue = false)] public string? Confidence { get; set; }
}

[DataContract]
public sealed class OfficialEvent
{
    [DataMember(IsRequired = true)] public long CapturedUtcTicks { get; set; }
    [DataMember(IsRequired = true)] public string Kind { get; set; } = string.Empty;
    [DataMember(IsRequired = true)] public string Detail { get; set; } = string.Empty;
    [DataMember(IsRequired = true)] public string Source { get; set; } = string.Empty;
    [DataMember(IsRequired = true)] public string Classification { get; set; } = string.Empty;
    [DataMember(EmitDefaultValue = false)] public string? Confidence { get; set; }
}
