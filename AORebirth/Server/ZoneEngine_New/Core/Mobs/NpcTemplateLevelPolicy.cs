namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using System.Collections.Generic;
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// A resolved template must contain stats for the selected level. Generic
    /// family/overlay interpolation runs in the data loader before this check.
    /// </summary>
    internal static class NpcTemplateLevelPolicy
    {
        internal static void RequireExactLevel(MobTemplate template, int? requestedLevel,
            IReadOnlyDictionary<int, int>? resolvedStats = null)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (!requestedLevel.HasValue) return;
            if (requestedLevel.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedLevel));

            var stats = resolvedStats ?? template.Stats;
            if (!stats.TryGetValue((int)CharacterStat.Level, out int templateLevel)
                || templateLevel != requestedLevel.Value)
            {
                throw new InvalidOperationException(
                    $"Mob template '{template.Hash}' has no exact stat variant for level {requestedLevel.Value}; "
                    + "changing only Level would retain another variant's stats.");
            }
        }
    }
}
