namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Text.Json.Serialization;

internal sealed record NpcEditorIdentity(int PlayfieldId, int IdentityType, int InstanceId, Guid Generation);

internal sealed record NpcEditorNpc(
    NpcEditorIdentity Identity, int ClientPlayfieldId, string Name, int Level,
    int? TemplateId, string? TemplateHash, string? SpawnHash, int? MonsterData, string SpawnSource, string[]? FamilyHashes = null);

internal sealed record NpcEditorRespawn(
    int Seconds, int Chance, bool RespawnsAfterDeath, string State, DateTime? NextSpawnUtc);

internal sealed record NpcEditorStat(
    int StatId, string Name, int? RuntimeBase, int? RuntimeBonus, int? RuntimeValue,
    int? ConfiguredValue, bool Editable, string? ConfiguredSource = null);

internal sealed record NpcEditorEquippedItem(
    int Slot, int InstanceId, int LowId, int HighId, int Quality, string Name, string ItemClass,
    string Role, int DefaultPosition, bool Replaceable, bool Removable);

internal sealed record NpcEditorConfiguredItem(int LowId, int HighId, string Name);
internal sealed record NpcEditorCatalogItem(int Id, string Name, int Quality, string ItemClass);
internal sealed record NpcEditorNano(int Id, string Name);
internal sealed record NpcEditorPoint(double X, double Y, double Z);

internal sealed record NpcEditorBrain(
    bool Attackable, bool HasBrain, bool IsAiBusy, bool PatrolEnabled, bool PatrolRun,
    NpcEditorPoint[] PatrolWaypoints, string[] Features, string? ScriptKey);

internal sealed record NpcEditorChange(
    Guid ChangeId, DateTime AtUtc, string Description, bool Reverted, bool Revertible,
    string Scope, string? Actor, string Before, string After, string? RevertedBy);

internal sealed record NpcEditorNpcDetails(
    NpcEditorNpc Npc, double X, double Y, double Z, string? SpawnPointKey, NpcEditorRespawn? Respawn,
    NpcEditorStat[] Stats, NpcEditorEquippedItem[] Equipment, NpcEditorConfiguredItem[] ConfiguredEquipment,
    NpcEditorNano[] UploadedNanos, NpcEditorNano[] ActiveNanos, NpcEditorBrain Brain, bool CanAddArmor,
    NpcEditorChange[] Changes);

internal sealed record NpcEditorEquipmentEdit(
    [property: JsonRequired] int Slot,
    [property: JsonRequired] int ExpectedInstanceId,
    [property: JsonRequired] int? ItemId,
    int? Quality);

internal sealed class NpcEditorRuntimeException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
