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

        public const string FindItemAction = "FindItem";
        public const string ReturnItemAction = "FindItem2";
        public const string RepairAction = "UseItemOnItem";

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

        /// <summary>
        /// The journal text as live sends it (QuestFullUpdate LongInfo): name, two breaks, the story and the red
        /// "Mission Objective" block, with its HTML. When empty the journal builds it from Name and Summary.
        /// </summary>
        public string? Description { get; set; }

        public QuestObjective Objective { get; set; } = new();

        /// <summary>Comma list of NoXp, NoCash, Failed, NoDelete, or "None".</summary>
        public string Flags { get; set; } = "None";

        public int? MissionBit { get; set; }

        /// <summary>
        /// The live game's own hashes for this step, where they differ from <see cref="Hash"/>: items name the quest by
        /// these in their criteria (HasActiveQuest) and SolveQuest (Health Regeneration Stim 297044: 82OD = JTEV).
        /// </summary>
        public string[]? Aliases { get; set; }

        /// <summary>Single follow-up quest hash, assigned when this one completes.</summary>
        public string? NextStep { get; set; }

        public QuestReward? Reward { get; set; }

        /// <summary>
        /// Can be done again after a cooldown (the daily DOJA chip turn-in). Without it a quest is done once, or
        /// restarted only by whoever gives it again.
        /// </summary>
        public QuestRecurring? Recurring { get; set; }

        /// <summary>Lowest level that may take and finish this quest (a DOJA chip's level band).</summary>
        public int? MinLevel { get; set; }

        /// <summary>Highest level that may take and finish this quest.</summary>
        public int? MaxLevel { get; set; }

        /// <summary>
        /// Items the player is handed as this step starts after the previous one (live, Surveillance Uplink: the
        /// Rebuilt HC-12 SecTec Monitor arrives with the Deliver the Bio Analyzing Computer reward). Granted with the
        /// previous step's reward, not shown in its reward box.
        /// </summary>
        public List<QuestRewardItem>? StartItems { get; set; }

        /// <summary>
        /// The journal compass waypoint live sends on the quest action: playfield and position (y is 0). Quests whose
        /// <see cref="Action"/> has no journal layout also take the action's version and slot type from here.
        /// </summary>
        public QuestWaypoint? Waypoint { get; set; }

        [JsonIgnore]
        public bool IsKill => string.Equals(Action, KillOneAction, StringComparison.Ordinal)
            || string.Equals(Action, KillMultipleAction, StringComparison.Ordinal);

        [JsonIgnore]
        public bool IsFindItem => string.Equals(Action, FindItemAction, StringComparison.Ordinal);

        [JsonIgnore]
        public bool IsReturnItem => string.Equals(Action, ReturnItemAction, StringComparison.Ordinal);

        [JsonIgnore]
        public bool IsRepair => string.Equals(Action, RepairAction, StringComparison.Ordinal);

        /// <summary>Flags "NoDelete": the player cannot remove it from the journal.</summary>
        [JsonIgnore]
        public bool IsNoDelete => Flags.Contains("NoDelete", StringComparison.Ordinal);

        /// <summary>Kills needed: Count for KillMultiple, otherwise one.</summary>
        [JsonIgnore]
        public int RequiredCount => string.Equals(Action, KillMultipleAction, StringComparison.Ordinal)
            ? Math.Max(1, Objective.Count ?? 1)
            : IsReturnItem ? 2 : 1;
    }

    public sealed class QuestObjective
    {
        /// <summary>Only KillMultiple: how many to kill.</summary>
        public int? Count { get; set; }

        /// <summary>NPC META (spawn) hash from the quest dump.</summary>
        public string? Npc { get; set; }

        /// <summary>
        /// More spawn hashes that count, for a kill objective over an area rather than one hash ("Kill 7 Robots in the
        /// junkyard": Cleaning Robots and the other junkyard robots).
        /// </summary>
        public string[]? Npcs { get; set; }

        /// <summary>[x, z, radius]: only kills of NPCs standing in this circle count.</summary>
        public float[]? Area { get; set; }

        /// <summary>
        /// Percent chance that each counted kill finishes the step early (Find a Bio Analyzing Computer: live it was
        /// found on the 2nd of up to 7 robots); <see cref="Count"/> kills always finish it.
        /// </summary>
        public int? FindChance { get; set; }

        public string? SpecItem { get; set; }

        public string? WorldItem { get; set; }
    }

    /// <summary>
    /// Live's quest action waypoint (QuestActionInfo PlayfieldId and Position): the compass points at
    /// <see cref="Position"/> while the player is in <see cref="Playfield"/>.
    /// </summary>
    public sealed class QuestWaypoint
    {
        public int Playfield { get; set; }

        /// <summary>[x, y, z]; live sends y as 0.</summary>
        public float[] Position { get; set; } = [];

        /// <summary>QuestActionInfo version live sent (24 talk/use, 23 return, 20 kill in area, 18 assemble, 6 deliver, 1 kill).</summary>
        public int Version { get; set; }

        /// <summary>Identity type of the action's UnknownId7 slot live sent (0xD2FC = 54012, 0xD2F1 = 54001).</summary>
        public int SlotType { get; set; }
    }

    public sealed class QuestReward
    {
        public string Hash { get; set; } = string.Empty;

        public int Credits { get; set; }

        public int Xp { get; set; }

        /// <summary>Also one full level's worth of XP at the player's level when it is paid (the DOJA chip reward).</summary>
        public bool FullLevelXp { get; set; }

        /// <summary>Also the side's tokens for the player's level tier, as the daily mission reward gives (clan/omni).</summary>
        public bool SideTokens { get; set; }

        /// <summary>Alternative item sets; one record is given, all its items together.</summary>
        public List<List<QuestRewardItem>>? Records { get; set; }
    }

    /// <summary>How a recurring quest waits before it can be taken again.</summary>
    public sealed class QuestRecurring
    {
        public const string PerAccount = "Account";
        public const string PerCharacter = "Character";

        /// <summary>From completion until the quest can be taken (and completed) again.</summary>
        public int CooldownSeconds { get; set; }

        /// <summary>"Account": one completion holds back every character on the account. "Character": only that one.</summary>
        public string Per { get; set; } = PerCharacter;

        /// <summary>
        /// Quests sharing one cooldown (each DOJA chip's quest: one chip a day, whichever). The quest's own hash when
        /// empty.
        /// </summary>
        public string? CooldownGroup { get; set; }

        /// <summary>Said when the cooldown holds the quest back. {time} is the time left.</summary>
        public string? CooldownText { get; set; }

        [JsonIgnore]
        public bool IsPerAccount => string.Equals(Per, PerAccount, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class QuestRewardItem
    {
        public int Id { get; set; }

        public int? HighId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Quality { get; set; } = 1;
    }
}
