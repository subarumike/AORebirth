namespace ZoneEngine_New.Core.Quests.Dungeons;

using System;
using System.Linq;
using ZoneEngine_New.Core.GameData;

/// <summary>Mission population references into the existing NPC catalog, not a second source of NPC definitions.</summary>
public sealed class DungeonPopulationPolicy
{
    public int SpawnPointChancePercent { get; set; }

    public string[] RegularNpcHashes { get; set; } = [];

    internal bool IsValid(IGameData gameData) => IsValid(gameData.HasMobTemplate);

    internal bool IsValid(Func<string, bool> hasMobTemplate)
        => SpawnPointChancePercent is >= 0 and <= 100
            && RegularNpcHashes is { Length: > 0 }
            && RegularNpcHashes.Distinct(StringComparer.Ordinal).Count() == RegularNpcHashes.Length
            && RegularNpcHashes.All(hash => !string.IsNullOrWhiteSpace(hash) && hasMobTemplate(hash));
}
