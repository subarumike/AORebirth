namespace ZoneEngine_New.Core.Mobs
{
    using System;
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// An extracted template's level range is placement metadata, not a proven
    /// interpolation rule for its HP, AC, attack, defence, nanos or rewards.
    /// Exact accepted variant adapters must supply the complete selected template.
    /// </summary>
    internal static class NpcTemplateLevelPolicy
    {
        internal static void RequireExactLevel(MobTemplate template, int? requestedLevel)
        {
            ArgumentNullException.ThrowIfNull(template);
            if (!requestedLevel.HasValue) return;
            if (requestedLevel.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedLevel));

            if (!template.Stats.TryGetValue((int)CharacterStat.Level, out int templateLevel)
                || templateLevel != requestedLevel.Value)
            {
                throw new InvalidOperationException(
                    $"Mob template '{template.Hash}' has no exact stat variant for level {requestedLevel.Value}; "
                    + "changing only Level would retain another variant's stats.");
            }
        }
    }
}
