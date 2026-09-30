namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One quest in GameData/Quests.json format. Template quests come from that file; generated quests
    /// use the same shape and are stored with the player.
    /// </summary>
    public sealed class QuestTemplate
    {
        /// <summary>Kill the objective NPC once.</summary>
        public const string KillOneAction = "KillOne";

        /// <summary>Kill <see cref="QuestObjective.Count"/> of the objective NPC.</summary>
        public const string KillMultipleAction = "KillMultiple";

        /// <summary>Select (target) the objective NPC.</summary>
        public const string TargetNpcAction = "TargetNpc";

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        public string Hash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int QasInstance { get; set; }

        /// <summary>"Solo" or "Team".</summary>
        public string Scope { get; set; } = "Solo";

        public string Action { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public QuestObjective Objective { get; set; } = new();

        /// <summary>Comma list of NoXp, NoCash, Failed, NoDelete, or "None".</summary>
        public string Flags { get; set; } = "None";

        public int? MissionBit { get; set; }

        /// <summary>Single follow-up quest hash, assigned when this one completes.</summary>
        public string? NextStep { get; set; }

        public QuestReward? Reward { get; set; }

        [JsonIgnore]
        public bool IsKill => string.Equals(Action, KillOneAction, StringComparison.Ordinal)
            || string.Equals(Action, KillMultipleAction, StringComparison.Ordinal);

        /// <summary>Flags "NoDelete": the player cannot remove it from the journal.</summary>
        [JsonIgnore]
        public bool IsNoDelete => Flags.Contains("NoDelete", StringComparison.Ordinal);

        /// <summary>Kills needed: Count for KillMultiple, otherwise one.</summary>
        [JsonIgnore]
        public int RequiredCount => string.Equals(Action, KillMultipleAction, StringComparison.Ordinal)
            ? Math.Max(1, Objective.Count ?? 1)
            : 1;
    }

    public sealed class QuestObjective
    {
        /// <summary>Only KillMultiple: how many to kill.</summary>
        public int? Count { get; set; }

        /// <summary>NPC META (spawn) hash from the quest dump.</summary>
        public string? Npc { get; set; }

        public string? SpecItem { get; set; }

        public string? WorldItem { get; set; }
    }

    public sealed class QuestReward
    {
        public string Hash { get; set; } = string.Empty;

        public int Credits { get; set; }

        public int Xp { get; set; }

        /// <summary>Alternative item sets; one record is given, all its items together.</summary>
        public List<List<QuestRewardItem>>? Records { get; set; }
    }

    public sealed class QuestRewardItem
    {
        public int Id { get; set; }

        public int? HighId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Quality { get; set; } = 1;
    }
}
