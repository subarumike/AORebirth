namespace ZoneEngine_New.Core.Playfield;

using System;
using System.Linq;
using AORebirth.Core.GameData;

/// <summary>Structural validation only. Editable placements do not require evidence approval.</summary>
internal static class SpawnContentValidation
{
    internal static bool IsValid(PlayfieldSpawnEntry? entry)
        => entry != null && !string.IsNullOrWhiteSpace(entry.HashText)
            && entry.MinLevel > 0 && entry.MaxLevel >= entry.MinLevel
            && entry.RespawnChance >= 0 && entry.RespawnChance <= 100 && entry.RespawnTime >= 0
            && ValidSite(entry.Position, entry.Radius)
            && (entry.AdditionalPoints == null || entry.AdditionalPoints.All(p => p != null && ValidSite(p.Position, p.Radius)));
    static bool ValidSite(float[]? position, float radius)
        => position is { Length: 3 } && position.All(float.IsFinite) && float.IsFinite(radius) && radius >= 0;
}
