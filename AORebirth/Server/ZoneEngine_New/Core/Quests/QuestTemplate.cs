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

        /// <summary>Finished by a tradeskill combine that makes the <see cref="QuestObjective.SpecItem"/> item.</summary>
        public const string AssembleAction = "Assemble";

        /// <summary>
        /// Finished by equipping the <see cref="QuestObjective.SpecItem"/> item (Install the implant: the Leg Implant
        /// worn after the Surgery Clinic opened implant swapping).
        /// </summary>
        public const string EquipAction = "Equip";

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        public string Hash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Journal title (retail ShortInfo); <see cref="Name"/> when absent.</summary>
        public string? Title { get; set; }

        /// <summary>
        /// The journal text as live sends it (QuestFullUpdate LongInfo): the title, two breaks, the story and the red
        /// "Mission Objective" block, with its HTML. When empty the journal builds it from Name and Summary.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>Journal icon; the action's retail icon when absent.</summary>
        public int? IconId { get; set; }

        public int QasInstance { get; set; }

        /// <summary>"Solo" or "Team".</summary>
        public string Scope { get; set; } = "Solo";

        public string Action { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

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
        /// A Quests.json quest that runs out this many seconds after it is assigned, with the journal timer, as a
        /// generated quest does: live's DOJA cooldown mission ("You can only turn in one DOJA chip today.", 64800 s),
        /// which the chips' HasActiveQuest refuses on while it runs. Null: it never expires. Not read for generated
        /// quests, which carry their own expiry.
        /// </summary>
        public int? ExpiresAfterSeconds { get; set; }

        /// <summary>
        /// Items the player is handed when this step is assigned, however it starts (a previous step, Knubot
        /// <c>{givequest}</c>, <c>.quests</c>): live, Surveillance Uplink, the Rebuilt HC-12 SecTec Monitor arrives with the
        /// step. Not part of any reward box.
        /// </summary>
        public List<QuestRewardItem>? StartItems { get; set; }

        /// <summary>
        /// The journal compass waypoint live sends on the quest action: playfield and a target or position (y is 0). The
        /// action's protocol values come from <see cref="Action"/> (QuestJournal), never from the content.
        /// </summary>
        public QuestWaypoint? Waypoint { get; set; }

        /// <summary>
        /// The compass point as dumped from live's journal ([{Playfield, X, Y, Z}]). <see cref="QuestCatalog"/> folds the
        /// first into <see cref="Waypoint"/> when none is written, so an action with a journal layout gets its point.
        /// </summary>
        public List<QuestDumpWaypoint>? Waypoints { get; set; }

        /// <summary>What did not resolve when Quests.json loaded (an item hash); null when nothing. The quest still runs.</summary>
        [JsonIgnore]
        public string? LoadProblem { get; set; }

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
        /// Percent chance that each counted kill finishes the step early (Find a Bio Analyzing Computer: live it was
        /// found on the 2nd of up to 7 robots); <see cref="Count"/> kills always finish it.
        /// </summary>
        public int? FindChance { get; set; }

        /// <summary>
        /// The quest's specific item, as an item hash (ItemTemplates.json). For Assemble it is the item a tradeskill
        /// combine must make to finish the step; for Equip the item to wear.
        /// </summary>
        public string? SpecItem { get; set; }

        public string? WorldItem { get; set; }
    }

    /// <summary>
    /// Live's quest action waypoint (QuestActionInfo PlayfieldId and Position): the compass points at the
    /// <see cref="Target"/>'s position, or at <see cref="Position"/>, while the player is in <see cref="Playfield"/>.
    /// </summary>
    public sealed class QuestWaypoint
    {
        public int Playfield { get; set; }

        /// <summary>
        /// What the compass points at, looked up when the journal is sent: an NPC hash (the NPC standing on the playfield,
        /// else its Spawns.json record) or an ItemTemplates.json hash of a statel placed by Dynels.dat. When it does
        /// not resolve, <see cref="Position"/> is used.
        /// </summary>
        public string? Target { get; set; }

        /// <summary>[x, y, z]; live sends y as 0. The point itself, or the fallback for <see cref="Target"/>.</summary>
        public float[] Position { get; set; } = [];

    }

    /// <summary>One dumped compass point (<see cref="QuestTemplate.Waypoints"/>).</summary>
    public sealed class QuestDumpWaypoint
    {
        public int Playfield { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }
    }

    public sealed class QuestReward
    {
        public string Hash { get; set; } = string.Empty;

        public int Credits { get; set; }

        public int Xp { get; set; }

        /// <summary>Alternative item sets; one record is given, all its items together.</summary>
        public List<List<QuestRewardItem>>? Records { get; set; }
    }

    /// <summary>
    /// An item a quest hands over. Quests.json names it by <see cref="Hash"/> (ItemTemplates.json) and
    /// <see cref="Quality"/>; <see cref="QuestCatalog"/> resolves that to <see cref="Id"/> and <see cref="HighId"/> at load.
    /// Generated quests store the ids themselves.
    /// </summary>
    public sealed class QuestRewardItem
    {
        public string? Hash { get; set; }

        public int Id { get; set; }

        public int? HighId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Quality { get; set; } = 1;

        /// <summary>The stack size to deliver (QuestRewards.json <c>Count</c>), or null for the item's own mint count.</summary>
        public int? Count { get; set; }
    }
}
