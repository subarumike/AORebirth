namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;

    public enum QuestSource
    {
        /// <summary>GameData/Quests.json template. Never expires.</summary>
        Template = 0,

        /// <summary>Generated in Quests.json format and stored once in generatedquests. Expires.</summary>
        Generated = 1,
    }

    /// <summary>Who a generated quest belongs to.</summary>
    public enum QuestOwnerType
    {
        Character = 0,
        Team = 1,
    }

    public enum QuestState
    {
        Active = 1,
        Completed = 2,
        Failed = 3,
        Expired = 4,

        /// <summary>The player deleted it from the journal.</summary>
        Abandoned = 5,
    }

    /// <summary>
    /// One quest a player holds: a <c>characterquests</c> row, with a generated quest's expiry and ACG data
    /// taken from its <c>generatedquests</c> row.
    /// </summary>
    public sealed class PlayerQuest
    {
        public required string QuestId { get; init; }

        public required QuestSource Source { get; init; }

        public required QuestTemplate Template { get; init; }

        public QuestState State { get; set; } = QuestState.Active;

        public int Progress { get; set; }

        public int RequiredCount { get; set; }

        public DateTime AssignedAtUtc { get; set; }

        /// <summary>Null never expires (template quests).</summary>
        public DateTime? ExpiresAtUtc { get; set; }

        /// <summary>Serialized ACG building generator data for quests that own a generated building.</summary>
        public string? AcgBuildingGeneratorJson { get; set; }

        public bool IsActive => State == QuestState.Active;

        public int Remaining => Math.Max(0, RequiredCount - Progress);
    }

    /// <summary>A player's quests by id, loaded from the database on first use.</summary>
    public sealed class QuestLog
    {
        public Dictionary<string, PlayerQuest> Quests { get; } = new(StringComparer.Ordinal);
    }
}
