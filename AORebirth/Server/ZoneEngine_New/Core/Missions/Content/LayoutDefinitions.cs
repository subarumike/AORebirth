namespace ZoneEngine.Core.Missions;

using System;
using ZoneEngine_New.Core.Missions;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.IO;

// Serialized field names belong to the existing editable Layouts.json contract.
// These records contain no world content or lifecycle/persistence algorithms.
internal enum MissionAcgWireCategory { Unknown, Door, Chest, Terminal }
internal enum MissionAcgRetargetCategory { Unknown, CharacterInstance, Playfield2Instance, ParentIdentityType, ParentIdentityInstance, DynelIdentityType, DynelIdentityInstance, BuildingIdentityType, BuildingIdentityInstance }
internal enum MissionAcgLayoutCompletenessState { CompleteSelectable = 1, StructurallyCompleteObjectiveIncomplete, IncompleteNonSelectable, ConflictingRejected }
internal sealed record MissionAcgIdentityRecord(int Type, int Instance);
internal sealed record MissionAcgPointRecord(float X, float Y, float Z);
internal sealed record MissionAcgRotationRecord(float X, float Y, float Z, float W);
internal sealed record MissionAcgRetargetSlotRecord(MissionAcgRetargetCategory Category, int Slot, int ByteOffset, int CapturedValue);
internal sealed record MissionAcgNpcTextureRecord(int Slot, int TextureId, int Unknown = 0);
internal sealed record MissionAcgNpcMeshRecord(int Position, int MeshId, int Unknown1, int Unknown2);

internal sealed class MissionAcgWireRecord
{
    public MissionAcgWireCategory Category { get; init; }
    public int Slot { get; init; }
    public string PacketHex { get; init; } = "";
    public string PacketSha256 => MissionAcgHash.ComputeSha256(CopyPacketBytes());
    public MissionAcgIdentityRecord CapturedIdentity { get; init; }
    public int? CapturedPlayfield2 { get; init; }
    public MissionAcgIdentityRecord CapturedParentIdentity { get; init; }
    public IReadOnlyList<MissionAcgRetargetSlotRecord> RetargetSlots { get; init; } = [];
    internal byte[] CopyPacketBytes() => Convert.FromHexString(PacketHex);
}

internal sealed class MissionAcgProvenanceRecord
{
    public string CaptureId { get; init; } = "";
    public string Source { get; init; } = "";
    public string Notes { get; init; } = "";
    public int CsvLine { get; init; }
    public long GlobalOrdinal { get; init; }
    public int Sequence { get; init; }
    public string Direction { get; init; } = "";
    public string CapturedUtc { get; init; } = "";
    public string MessageType { get; init; } = "";
    public string PreservationStatus { get; init; } = "";
    public int RawPacketLength { get; init; }
    public string RawPacketSha256 { get; init; } = "";
    public string ParseStatus { get; init; } = "";
}

internal sealed class MissionAcgDynelRecord
{
    public MissionAcgWireCategory Category { get; init; }
    public int Slot { get; init; }
    public MissionAcgIdentityRecord CapturedIdentity { get; init; }
    public int? CapturedPlayfield2 { get; init; }
    public MissionAcgIdentityRecord CapturedParentIdentity { get; init; }
    public MissionAcgPointRecord Position { get; init; }
    public MissionAcgRotationRecord Heading { get; init; }
    public int TemplateId { get; init; }
    public string Name { get; init; } = "";
    public MissionAcgWireRecord Wire { get; init; }
    public IReadOnlyList<MissionAcgProvenanceRecord> Provenance { get; init; } = [];
}

internal class MissionLayoutPlacement
{
    public MissionAcgIdentityRecord CapturedIdentity { get; init; }
    public int? CapturedPlayfield2 { get; init; }
    public MissionAcgIdentityRecord CapturedParentIdentity { get; init; }
    public MissionAcgPointRecord Position { get; init; }
    public MissionAcgRotationRecord Heading { get; init; }
    public int TemplateId { get; init; }
    public string Name { get; init; } = "";
    public string RawPacketHex { get; init; } = "";
    public string RawPacketSha256 => RawPacketHex.Length == 0 ? "" : MissionAcgHash.ComputeSha256(CopyRawPacket());
    public IReadOnlyList<MissionAcgProvenanceRecord> Provenance { get; init; } = [];
    internal byte[] CopyRawPacket() => Convert.FromHexString(RawPacketHex);
}

internal sealed class MissionAcgNpcSlotRecord : MissionLayoutPlacement
{
    public int Slot { get; init; }
    public int MonsterData { get; init; }
    public int CapturedLevel { get; init; }
    public int CapturedHealth { get; init; }
    public int CapturedHealthDamage { get; init; }
    public int Scale { get; init; }
    public int? HeadMesh { get; init; }
    public string Role { get; init; } = "";
    public IReadOnlyList<MissionAcgNpcTextureRecord> Textures { get; init; } = [];
    public IReadOnlyList<MissionAcgNpcMeshRecord> Meshes { get; init; } = [];
}
internal sealed class MissionAcgObjectiveSlotRecord : MissionLayoutPlacement
{
    public int Slot { get; init; }
    public IReadOnlyList<MissionRollType> CompatibleMissionTypes { get; init; } = [];
}
internal sealed class MissionAcgExitRecord : MissionLayoutPlacement { }

internal sealed class MissionAcgCompatibilityRecord
{
    public int MinimumMissionQuality { get; init; }
    public int MaximumMissionQuality { get; init; }
    public IReadOnlyList<MissionRollType> MissionTypes { get; init; } = [];
    internal bool Supports(MissionRollType type, int quality) => quality >= MinimumMissionQuality && quality <= MaximumMissionQuality && MissionTypes.Contains(type);
}
internal sealed class MissionAcgCaptureCountsRecord
{
    public int DoorObservationCount { get; init; }
    public int ChestObservationCount { get; init; }
    public int TerminalObservationCount { get; init; }
    public int SimpleCharObservationCount { get; init; }
    public int NpcObservationCount { get; init; }
    public int ObjectiveObservationCount { get; init; }
    public int NormalizedDoorSlotCount { get; init; }
    public int NormalizedChestSlotCount { get; init; }
    public int NormalizedTerminalSlotCount { get; init; }
    public int NormalizedNpcSlotCount { get; init; }
    public int NormalizedObjectiveSlotCount { get; init; }
}
internal sealed class MissionAcgCompletenessRecord
{
    public MissionAcgLayoutCompletenessState State { get; init; }
    public bool HasGeneratorPayload { get; init; }
    public bool HasBuildingIdentity { get; init; }
    public bool HasEntryPoint { get; init; }
    public bool HasExit { get; init; }
    public bool HasDoorWire { get; init; }
    public bool HasChestWire { get; init; }
    public bool HasNpcSlots { get; init; }
    public bool HasObjectiveSlots { get; init; }
    public bool HasLifecycleCorrelation { get; init; }
    internal bool IsSelectionComplete => State == MissionAcgLayoutCompletenessState.CompleteSelectable
        && new[] { HasGeneratorPayload, HasBuildingIdentity, HasEntryPoint, HasExit, HasDoorWire, HasChestWire, HasNpcSlots, HasObjectiveSlots, HasLifecycleCorrelation }.All(value => value);
}
internal sealed record MissionAcgLayoutExclusion(string LayoutId, int SourcePlayfield2, string Reason);

internal sealed class MissionAcgLayoutBundle
{
    internal const int CurrentFormatVersion = 1;
    public int BundleFormatVersion { get; init; }
    public string LayoutId { get; init; } = "";
    public int SourcePlayfield2 { get; init; }
    public MissionAcgIdentityRecord BuildingIdentity { get; init; }
    public byte[] GeneratorPayload { private get; init; } = [];
    public string GeneratorPayloadSha256 => MissionAcgHash.ComputeSha256(GeneratorPayload);
    public string ExpectedGeneratorPayloadSha256 { get; init; } = "";
    public MissionAcgPointRecord EntryPoint { get; init; }
    public MissionAcgExitRecord Exit { get; init; }
    public IReadOnlyList<MissionAcgDynelRecord> Dynels { get; init; } = [];
    public IReadOnlyList<MissionAcgNpcSlotRecord> NpcSlots { get; init; } = [];
    public IReadOnlyList<MissionAcgObjectiveSlotRecord> ObjectiveSlots { get; init; } = [];
    public MissionAcgCaptureCountsRecord CaptureCounts { get; init; }
    public MissionAcgIdentityRecord CapturedPlayerIdentity { get; init; }
    public MissionAcgCompatibilityRecord Compatibility { get; init; }
    public IReadOnlyList<MissionAcgProvenanceRecord> Provenance { get; init; } = [];
    public MissionAcgCompletenessRecord Completeness { get; init; }
    public bool IsSelectable { get; init; }
    public string SelectionExclusionReason { get; init; } = "";
    internal IReadOnlyList<MissionAcgWireRecord> WireRecords => Dynels.Select(value => value.Wire).Where(value => value != null).ToArray();
    internal IReadOnlyList<MissionAcgWireRecord> Doors => WireRecords.Where(value => value.Category == MissionAcgWireCategory.Door).ToArray();
    internal IReadOnlyList<MissionAcgWireRecord> Chests => WireRecords.Where(value => value.Category == MissionAcgWireCategory.Chest).ToArray();
    internal IReadOnlyList<MissionAcgWireRecord> Terminals => WireRecords.Where(value => value.Category == MissionAcgWireCategory.Terminal).ToArray();
    internal IReadOnlyList<MissionRollType> CompatibleMissionTypes => Compatibility.MissionTypes;
    internal byte[] CopyGeneratorPayload() => GeneratorPayload.ToArray();
    internal bool SupportsMissionType(MissionRollType type) => CompatibleMissionTypes.Contains(type);
    internal bool SupportsMission(MissionRollType type, int quality) => Compatibility.Supports(type, quality);
    internal void ValidateShape()
    {
        if (BundleFormatVersion <= 0 || string.IsNullOrWhiteSpace(LayoutId) || SourcePlayfield2 <= 0
            || Completeness == null || Compatibility == null || !Enum.IsDefined(Completeness.State)
            || Compatibility.MinimumMissionQuality <= 0 || Compatibility.MaximumMissionQuality < Compatibility.MinimumMissionQuality
            || Compatibility.MissionTypes == null || IsSelectable == !string.IsNullOrWhiteSpace(SelectionExclusionReason)
            || Dynels == null || NpcSlots == null || ObjectiveSlots == null
            || Dynels.Any(value => value == null || value.Slot < 0) || NpcSlots.Any(value => value == null || value.Slot < 0)
            || ObjectiveSlots.Any(value => value == null || value.Slot < 0 || value.CompatibleMissionTypes == null))
            throw new InvalidDataException("Invalid mission layout structure.");
        foreach (var wire in WireRecords)
            if (wire.Slot < 0 || wire.Category == MissionAcgWireCategory.Unknown || !Enum.IsDefined(wire.Category)
                || wire.RetargetSlots == null || wire.RetargetSlots.Any(slot => slot == null || slot.Slot < 0 || slot.ByteOffset < 0
                    || slot.Category == MissionAcgRetargetCategory.Unknown || !Enum.IsDefined(slot.Category)))
                throw new InvalidDataException("Invalid mission layout wire or retarget slot.");
    }
}

internal static class MissionAcgHash
{
    internal static string ComputeSha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static byte[] ParseHex(string hex, string parameterName) => Convert.FromHexString(hex);
    internal static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);
    internal static int ReadInt32BigEndian(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, sizeof(int)));
}
