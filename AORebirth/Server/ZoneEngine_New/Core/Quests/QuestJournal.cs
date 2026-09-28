namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Missions;

    /// <summary>
    /// Client journal entries for Quests.json quests, laid out like the retail Rubi-Ka tutorial chain
    /// (KION "Kill a Leet" -> KLTR "Kill three Leets" -> FDTH "Find the Secondhand Peddler", captured 2026-09-27).
    /// Retail flow: assignment sends one QuestFullUpdate with the announce byte set; zone entry re-sends it with the
    /// byte clear; kills send nothing; completion sends the QuestMessage delete, then the next step's assignment.
    /// Field meanings from the client's Quest_t reader (Gamecode.dll FUN_100abc80), RewardBox_t reader (FUN_10086e85)
    /// and the capture:
    /// Version 15, Flags 2 (+0xa4; 0x100 team mission, 0x400 no delete). ShortInfo = name; LongInfo = name, two
    /// breaks, description. UnknownId1 = CanbeAffected:giver NPC hash (retail "NEGD", the Guide).
    /// RewardDescriptorVersion 6, CashReward, ExperienceReward, two empty lists, MissionItemData = reward item icons.
    /// UnknownHash1 = reward hash, Unknown14 = 220, UnknownId2 = the player, MissionIconId (+0x48) by action,
    /// Unknown20/21 = 7200. One QuestAction (+0x28) naming the objective NPC hash and carrying the expiry.
    /// Unknown23 = kill count (KillMultiple only), Unknown24 = QasInstance, Unknown27 = 7. Unknown28 is the
    /// message's trailing announce byte.
    /// </summary>
    internal static class QuestJournal
    {
        /// <summary>Quest flags bit for a team mission (Quest_t +0xa4).</summary>
        const int TeamMissionFlag = 0x100;

        /// <summary>
        /// Quest flags bit that makes the client refuse deletion: N3Msg_RemoveQuest shows client text 0x66 instead of
        /// sending the delete request when it is set.
        /// </summary>
        const int NoDeleteFlag = 0x400;

        /// <summary>
        /// Journal identity instances for these quests: a fixed high byte, clear of the 0x55xxxxxx instances the
        /// captured authored quests use, with the low 24 bits hashed from the quest id.
        /// </summary>
        const int InstancePrefix = 0x5A000000;

        /// <summary>Icon for quests with no captured retail action (the captured Arete talk quest's icon).</summary>
        const int DefaultIconId = 244818;

        /// <summary>Retail sends the quest action's expiry five days after assignment, even for chain quests.</summary>
        static readonly TimeSpan DisplayedLifetime = TimeSpan.FromDays(5);

        /// <summary>Retail action layout per Quests.json action, from the captured KION/KLTR/FDTH journals.</summary>
        sealed record ActionLayout(int Version, int IconId, int SlotType, bool TargetInSlot3);

        static readonly Dictionary<string, ActionLayout> Layouts = new(StringComparer.Ordinal)
        {
            // KION: version 1, target NpcHash:LE01 in UnknownId2, slot 0xD2FC.
            [QuestTemplate.KillOneAction] = new(1, 11330, 0xD2FC, TargetInSlot3: false),
            // KLTR: version 20, target 0:LE01 in UnknownId3, slot 0xD2FC.
            [QuestTemplate.KillMultipleAction] = new(20, 11330, 0xD2FC, TargetInSlot3: true),
            // FDTH: version 16, target NpcHash:SOPD in UnknownId2, slot 0xD2F1.
            ["TargetNpc"] = new(16, 11335, 0xD2F1, TargetInSlot3: false),
        };

        internal static int JournalInstance(string questId)
        {
            // FNV-1a over the id, folded to 24 bits: stable across sessions for the same quest.
            uint hash = 2166136261;
            foreach (char c in questId)
                hash = (hash ^ c) * 16777619;
            return InstancePrefix | (int)(hash & 0x00FFFFFF);
        }

        /// <summary>
        /// Adds the quest to the client journal. The client appends every QuestFullUpdate entry to its quest list
        /// without replacing one with the same id (Gamecode.dll FUN_10056abb -> FUN_10045861), and a delete removes
        /// only the first match (FUN_10056a7e). So each quest is sent once per zone: on assignment with
        /// <paramref name="announce"/> (the client prints "You got a new mission" and refreshes the journal), and
        /// silently on zone entry. Kill progress is not re-sent; it is reported by feedback text.
        /// </summary>
        internal static void Send(Player player, PlayerQuest quest, bool announce)
        {
            if (player.Session == null)
                return;

            player.Session.Send(Build(player, quest, announce));
        }

        internal static void Delete(Player player, PlayerQuest quest)
        {
            player.Session?.Send(new QuestMessage
            {
                Identity = player.Identity,
                Unknown = 0,
                Action = QuestAction.Delete,
                Unknown1 = 0,
                Mission = new Identity { Type = IdentityType.Mission, Instance = JournalInstance(quest.QuestId) },
                Unknown2 = 0,
                Unknown3 = 0
            });
        }

        static QuestFullUpdateMessage Build(Player player, PlayerQuest quest, bool announce)
        {
            QuestTemplate template = quest.Template;
            QuestReward? reward = template.Reward;
            int journalInstance = JournalInstance(quest.QuestId);
            Layouts.TryGetValue(template.Action, out ActionLayout? layout);

            // Dungeon quests (accepted terminal missions): the terminal's icon, and an action whatever the objective,
            // because the action carries the waypoint to the dungeon entrance.
            bool isDungeon = Dungeons.QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out Dungeons.QuestDungeonParameters dungeon);
            if (isDungeon)
                layout ??= new ActionLayout(DungeonActionVersion(dungeon.MissionType), DefaultIconId, 0xD2FC, TargetInSlot3: false);

            // Retail's per-quest slot pair: the action's UnknownId7 instance and UnknownArray1's entry share their low
            // 27 bits. Their meaning is unknown; any stable value per quest is sent.
            int slot = 0x04000000 | (journalInstance & 0x00FFFFFF);

            int flags = 2;
            if (template.IsNoDelete)
                flags |= NoDeleteFlag;
            if (string.Equals(template.Scope, "Team", StringComparison.OrdinalIgnoreCase))
                flags |= TeamMissionFlag;

            var entry = new Quest
            {
                QuestId = new Identity { Type = IdentityType.Mission, Instance = journalInstance },
                Version = 15,
                Flags = flags,
                ShortInfo = template.Name,
                LongInfo = LongInfo(template),
                // Retail names the giver NPC here; Quests.json has no giver, so the instance stays 0.
                UnknownId1 = new Identity { Type = IdentityType.CanbeAffected, Instance = 0 },
                RewardDescriptorVersion = 6,
                CashReward = reward?.Credits ?? 0,
                ExperienceReward = reward?.Xp ?? 0,
                Unknown9 = 0x3F1,
                Unknown10 = 0x3F1,
                MissionItemData = RewardItems(reward),
                UnknownHash1 = reward?.Hash ?? string.Empty,
                Unknown14 = 220,
                UnknownId2 = Copy(player.Identity),
                MissionIconId = isDungeon && dungeon.MissionIconId > 0 ? dungeon.MissionIconId : layout?.IconId ?? DefaultIconId,
                Unknown20 = 7200,
                Unknown21 = 7200,
                QuestActions = layout == null ? [] : [Action(player, layout, template, quest, slot, isDungeon ? dungeon : null)],
                PlayerIds = [Copy(player.Identity)],
                UnknownArray1 = [slot],
                UnknownArray2 = [],
                CharacterInfos = [],
                Unknown22 = 6,
                PlayerIds2 = [Copy(player.Identity)],
                Unknown23 = string.Equals(template.Action, QuestTemplate.KillMultipleAction, StringComparison.Ordinal)
                    ? template.RequiredCount : 0,
                Unknown24 = template.QasInstance,
                UnknownId3 = new Identity(),
                QuestIdentities = [],
                Unknown27 = 7,
                FactionInfos = [],
                Unknown28 = announce ? (byte)1 : (byte)0
            };

            return new QuestFullUpdateMessage
            {
                Identity = Copy(player.Identity),
                Unknown = 1,
                Quests = [entry]
            };
        }

        /// <summary>Quest action version the generated-mission journal used per terminal roll type.</summary>
        static int DungeonActionVersion(int missionType) => missionType switch { 2 => 15, 4 => 8, _ => 16 };

        static QuestActionInfo Action(Player player, ActionLayout layout, QuestTemplate template, PlayerQuest quest, int slot,
            Dungeons.QuestDungeonParameters? dungeon)
        {
            int target = FourCc(template.Objective.Npc);
            DateTime expires = quest.ExpiresAtUtc ?? quest.AssignedAtUtc + DisplayedLifetime;
            uint expiresOnClient = ClientExpiry(player, DateTime.SpecifyKind(expires, DateTimeKind.Utc), DateTime.UtcNow);

            return new QuestActionInfo
            {
                Version = layout.Version,
                Action = new Identity(),
                UnknownId1 = new Identity(),
                UnknownId2 = layout.TargetInSlot3 ? new Identity() : new Identity { Type = IdentityType.NpcHash, Instance = target },
                UnknownId3 = layout.TargetInSlot3 ? new Identity { Type = 0, Instance = target } : new Identity(),
                UnknownId4 = new Identity(),
                UnknownId5 = new Identity(),
                UnknownId6 = new Identity(),
                // Retail's four bytes here are the big-endian Unix time the quest expires.
                UnknownHash1 = BigEndianChars(expiresOnClient),
                UnknownId7 = new Identity { Type = (IdentityType)layout.SlotType, Instance = 0x18000000 | slot },
                // Waypoint: the dungeon entrance's playfield and position, with the entrance building's template pair.
                PlayfieldId = dungeon == null
                    ? new Identity()
                    : new Identity { Type = (IdentityType)dungeon.DestinationType, Instance = dungeon.EntrancePlayfield },
                Unknown10 = dungeon?.BuildingLowId ?? 0,
                Unknown11 = dungeon?.BuildingHighId ?? 0,
                Position = dungeon == null
                    ? null
                    : new SmokeLounge.AOtomation.Messaging.GameData.Vector3(dungeon.EntranceX, dungeon.EntranceY, dungeon.EntranceZ)
            };
        }

        /// <summary>
        /// The expiry in the client's clock, which the journal counts "time left" against. Our GameTime starts that
        /// clock at ClientClockBaseSeconds when the session's clock was synchronized (at world entry), so the
        /// client's now is base + seconds since then; the expiry is that plus the quest's remaining seconds.
        /// A Unix time here showed as thousands of days left.
        /// </summary>
        static uint ClientExpiry(Player player, DateTime expiresUtc, DateTime nowUtc)
        {
            if (expiresUtc <= nowUtc)
                return 0;

            DateTime synchronized = player.Session is Network.IGameTimeSession { GameTimeSynchronizedAtUtc: DateTime at } ? at : nowUtc;
            long clientNow = MissionRollPolicy.Current.ClientClockBaseSeconds + (long)Math.Max(0, (nowUtc - synchronized).TotalSeconds);
            long expires = clientNow + (long)Math.Ceiling((expiresUtc - nowUtc).TotalSeconds);
            return (uint)Math.Min(expires, int.MaxValue);
        }

        static string LongInfo(QuestTemplate template)
        {
            var text = new StringBuilder(template.Name);
            if (!string.IsNullOrEmpty(template.Summary))
            {
                text.Append("<BR><BR>").Append(Capitalize(template.Summary));
                if (!template.Summary.EndsWith('.') && !template.Summary.EndsWith('!') && !template.Summary.EndsWith('?'))
                    text.Append('.');
            }
            return text.ToString();
        }

        static MissionItemReward[] RewardItems(QuestReward? reward)
        {
            if (reward?.Records is not { Count: > 0 } records)
                return [];

            // Records are alternatives; the journal shows the first set.
            var items = new List<MissionItemReward>();
            foreach (QuestRewardItem item in records[0])
                items.Add(new MissionItemReward { LowId = item.Id, HighId = item.HighId ?? item.Id, Ql = Math.Max(1, item.Quality) });
            return items.ToArray();
        }

        /// <summary>A 4-character hash ("LE01") as the big-endian int retail sends in identity instances.</summary>
        static int FourCc(string? hash)
        {
            if (string.IsNullOrEmpty(hash))
                return 0;
            int value = 0;
            for (int i = 0; i < 4; i++)
                value = (value << 8) | (i < hash.Length ? (byte)hash[i] : 0);
            return value;
        }

        /// <summary>Four bytes as the byte-per-char string the fixed 4-byte hash fields serialize.</summary>
        static string BigEndianChars(uint value)
            => new(new[] { (char)(value >> 24), (char)((value >> 16) & 0xFF), (char)((value >> 8) & 0xFF), (char)(value & 0xFF) });

        static string Capitalize(string text)
            => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

        static Identity Copy(Identity value) => new() { Type = value.Type, Instance = value.Instance };
    }
}
