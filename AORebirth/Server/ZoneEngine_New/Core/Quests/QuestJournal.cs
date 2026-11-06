namespace ZoneEngine_New.Core.Quests
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using AORebirth.Core.GameData;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Client journal entries for Quests.json quests, laid out like the retail Rubi-Ka tutorial chain
    /// (KION "Kill a Leet" -> KLTR "Kill three Leets" -> FDTH "Find the Secondhand Peddler", captured 2026-09-27).
    /// Retail flow: assignment sends one QuestFullUpdate with the announce byte set; zone entry re-sends it with the
    /// byte clear; kills send nothing; completion sends CharacterAction 0x3b, the QuestMessage delete, then the next
    /// step's assignment.
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

        /// <summary>
        /// Longest ShortInfo the client can hold. Quest_t reads it unbounded into a 32-byte field at +0x08 and then
        /// terminates at +0x27 (Gamecode.dll FUN_100abc80); a longer name overwrites the fields after it and crashes
        /// the client. The full name still goes in LongInfo.
        /// </summary>
        const int MaxShortInfoLength = 31;

        /// <summary>Retail sends the quest action's expiry five days after assignment, even for chain quests.</summary>
        static readonly TimeSpan DisplayedLifetime = TimeSpan.FromDays(5);

        /// <summary>Retail action layout per Quests.json action, from the captured KION/KLTR/FDTH journals.</summary>
        sealed record ActionLayout(int Version, int IconId, int SlotType, bool TargetInSlot3, bool NamesTarget = true);

        /// <summary>
        /// Live's waypoint identity type and the two values it sends beside every Quests.json waypoint (captured
        /// Arete Landing journals, 2026-09-29: PlayfieldId Playfield2:6553, Unknown10 = Unknown11 = 100000).
        /// </summary>
        const int WaypointPlayfieldType = 0x9C50;
        const int WaypointUnknown = 100000;

        static readonly Dictionary<string, ActionLayout> Layouts = new(StringComparer.Ordinal)
        {
            // KION: version 1, target NpcHash:LE01 in UnknownId2, slot 0xD2FC.
            [QuestTemplate.KillOneAction] = new(1, 11330, 0xD2FC, TargetInSlot3: false),
            // KLTR: version 20, target 0:LE01 in UnknownId3, slot 0xD2FC.
            [QuestTemplate.KillMultipleAction] = new(20, 11330, 0xD2FC, TargetInSlot3: true),
            // FDTH: version 16, target NpcHash:SOPD in UnknownId2, slot 0xD2F1.
            ["TargetNpc"] = new(16, 11335, 0xD2F1, TargetInSlot3: false),
        };

        /// <summary>
        /// The action live sends for a Quests.json action that has no journal layout of its own, used only to carry a
        /// waypoint (captured journals, PR #46): talk and use 24, return 23, assemble 18, deliver 6, use on an object 8.
        /// The protocol values live here, by action, not in the content.
        /// </summary>
        static readonly Dictionary<string, ActionLayout> WaypointLayouts = new(StringComparer.Ordinal)
        {
            ["TalkTo"] = new(24, DefaultIconId, 0xD2FC, TargetInSlot3: false, NamesTarget: false),
            ["UseItemOnCharacter"] = new(24, DefaultIconId, 0xD2FC, TargetInSlot3: false, NamesTarget: false),
            ["UseItemOnCharacterDev"] = new(24, DefaultIconId, 0xD2FC, TargetInSlot3: false, NamesTarget: false),
            ["ReturnToQuestGiver"] = new(23, DefaultIconId, 0xD2FC, TargetInSlot3: false, NamesTarget: false),
            [QuestTemplate.AssembleAction] = new(18, DefaultIconId, 0xD2FC, TargetInSlot3: false, NamesTarget: false),
            [QuestTemplate.ReturnItemAction] = new(6, DefaultIconId, 0xD2FC, TargetInSlot3: false, NamesTarget: false),
            ["BringItemHere"] = new(6, DefaultIconId, 0xD2FC, TargetInSlot3: false, NamesTarget: false),
            [QuestTemplate.RepairAction] = new(8, DefaultIconId, 0xD2FC, TargetInSlot3: false, NamesTarget: false),
        };

        /// <summary>
        /// Spawns.json record and Dynels.dat statel positions per (playfield, target hash), as [x0, z0, x1, z1, ...]: static
        /// GameData, read once.
        /// </summary>
        static readonly System.Collections.Concurrent.ConcurrentDictionary<(int Playfield, string Hash), float[]> StaticTargets = new();

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

        /// <summary>
        /// Tells the client the quest ended (<see cref="CharacterActionType.MissionAccomplished"/> or
        /// <see cref="CharacterActionType.QuestExpired"/>); the client prints the text itself. Send before
        /// <see cref="Delete"/>: the client looks the quest up, so after the delete only the sound plays.
        /// </summary>
        internal static void SendEnded(Player player, PlayerQuest quest, CharacterActionType action)
        {
            int instance = JournalInstance(quest.QuestId);
            player.Session?.Send(new CharacterActionMessage
            {
                Identity = player.Identity,
                Action = action,
                Target = new Identity { Type = IdentityType.Mission, Instance = instance },
                Parameter1 = (int)IdentityType.Mission,
                Parameter2 = instance
            });
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

            // A quest whose Action has no journal layout (talk, use, deliver, return, assemble) still gets live's
            // action when it has a waypoint: the action is what carries the compass point.
            if (layout == null && template.Waypoint is { Playfield: > 0 })
                WaypointLayouts.TryGetValue(template.Action, out layout);

            // Dungeon quests (accepted terminal missions): the terminal's icon, and an action whatever the objective,
            // because the action carries the waypoint to the dungeon entrance.
            bool isDungeon = Dungeons.QuestDungeonParameters.TryParse(quest.AcgBuildingGeneratorJson, out Dungeons.QuestDungeonParameters dungeon);
            if (isDungeon)
                layout = new ActionLayout(MissionRollPolicy.Current.Action((MissionRollType)dungeon.MissionType),
                    DefaultIconId, dungeon.MissionType == (int)MissionRollType.FindPerson ? 0xD2F1 : 0xD2FC, TargetInSlot3: false);

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
                ShortInfo = ShortName(string.IsNullOrEmpty(template.Title) ? template.Name : template.Title),
                LongInfo = LongInfo(template),
                // Retail names the giver NPC here; Quests.json has no giver, so the instance stays 0.
                UnknownId1 = new Identity { Type = IdentityType.CanbeAffected, Instance = 0 },
                RewardDescriptorVersion = 6,
                CashReward = reward?.Credits ?? 0,
                ExperienceReward = reward?.Xp ?? 0,
                Unknown9 = 0x3F1,
                Unknown10 = 0x3F1,
                MissionItemData = RewardItems(player, quest),
                UnknownHash1 = reward?.Hash ?? string.Empty,
                Unknown14 = 220,
                UnknownId2 = Copy(player.Identity),
                MissionIconId = isDungeon && dungeon.MissionIconId > 0 ? dungeon.MissionIconId
                    : template.IconId is int icon and > 0 ? icon
                    : layout?.IconId ?? DefaultIconId,
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
                // Original Find Person capture 20261011-215642, IN32: the exact person is also listed with flag 64.
                QuestIdentities = isDungeon && dungeon.MissionType == (int)MissionRollType.FindPerson && dungeon.ObjectiveNpc != null
                    ? [new QuestIdentity { Unknown1 = dungeon.ObjectiveNpc.Identity, Unknown2 = 64 }]
                    : [],
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

        static QuestActionInfo Action(Player player, ActionLayout layout, QuestTemplate template, PlayerQuest quest, int slot,
            Dungeons.QuestDungeonParameters? dungeon)
        {
            int target = FourCc(template.Objective.Npc);
            var objectiveItem = template.IsFindItem ? dungeon?.ObjectiveItem : null;
            Identity actionIdentity = objectiveItem?.Identity ?? new Identity();
            Identity destinationIdentity = new();
            Identity npcIdentity = template.IsFindItem || layout.TargetInSlot3 || !layout.NamesTarget
                ? new Identity() : new Identity { Type = IdentityType.NpcHash, Instance = target };
            if (dungeon != null)
            {
                switch ((MissionRollType)dungeon.MissionType)
                {
                    // Completed Kill Person and accepted Find Person captures bind the concrete NPC in slot 2.
                    case MissionRollType.KillPerson:
                    case MissionRollType.FindPerson:
                        if (dungeon.ObjectiveNpc != null)
                        {
                            npcIdentity = dungeon.ObjectiveNpc.Identity;
                        }
                        break;
                    case MissionRollType.FindItem:
                        npcIdentity = new Identity();
                        break;
                    // Retail UseItemOnItem has the source tool in Action and exact world target in slot 1.
                    case MissionRollType.RepairMachine:
                        actionIdentity = dungeon.ObjectiveTool?.Identity ?? new Identity();
                        destinationIdentity = dungeon.ObjectiveItem?.Identity ?? new Identity();
                        npcIdentity = new Identity();
                        break;
                    // Existing captured Return Item action projects the mission item and issuing terminal.
                    case MissionRollType.FindItemReturn:
                        actionIdentity = dungeon.ObjectiveItem?.Identity ?? new Identity();
                        destinationIdentity = dungeon.OriginTerminal;
                        npcIdentity = new Identity();
                        break;
                }
            }
            DateTime expires = quest.ExpiresAtUtc ?? quest.AssignedAtUtc + DisplayedLifetime;
            uint expiresOnClient = ClientExpiry(player, DateTime.SpecifyKind(expires, DateTimeKind.Utc), DateTime.UtcNow);

            // Waypoint: the dungeon entrance for terminal missions, else the quest's captured live waypoint.
            QuestWaypoint? waypoint = dungeon == null && template.Waypoint is { Playfield: > 0 } w ? w : null;
            SmokeLounge.AOtomation.Messaging.GameData.Vector3? waypointPoint = waypoint == null ? null : WaypointPoint(player, waypoint, template, layout.Version);
            if (waypointPoint == null)
                waypoint = null;
            Identity waypointPlayfield = dungeon != null
                ? new Identity { Type = (IdentityType)dungeon.DestinationType, Instance = dungeon.EntrancePlayfield }
                : waypoint != null
                    ? new Identity { Type = (IdentityType)WaypointPlayfieldType, Instance = waypoint.Playfield }
                    : new Identity();
            SmokeLounge.AOtomation.Messaging.GameData.Vector3? waypointPosition = dungeon != null
                ? new SmokeLounge.AOtomation.Messaging.GameData.Vector3(dungeon.EntranceX, dungeon.EntranceY, dungeon.EntranceZ)
                : waypointPoint;

            return new QuestActionInfo
            {
                Version = layout.Version,
                Action = actionIdentity,
                UnknownId1 = destinationIdentity,
                UnknownId2 = npcIdentity,
                UnknownId3 = !template.IsFindItem && layout.TargetInSlot3 ? new Identity { Type = 0, Instance = target } : new Identity(),
                UnknownId4 = new Identity(),
                UnknownId5 = new Identity(),
                UnknownId6 = new Identity(),
                // Retail's four bytes here are the big-endian Unix time the quest expires.
                UnknownHash1 = BigEndianChars(expiresOnClient),
                UnknownId7 = new Identity { Type = (IdentityType)layout.SlotType, Instance = 0x18000000 | slot },
                // Waypoint: playfield and position; a dungeon entrance also carries the entrance building's template pair.
                PlayfieldId = waypointPlayfield,
                Unknown10 = dungeon?.BuildingLowId ?? (waypoint != null ? WaypointUnknown : 0),
                Unknown11 = dungeon?.BuildingHighId ?? (waypoint != null ? WaypointUnknown : 0),
                Position = waypointPosition
            };
        }

        /// <summary>
        /// The expiry in the client's clock, which the journal counts "time left" against. The session's last GameTime
        /// gave the client its server seconds, which it advances in real time, so the client's now is that plus the
        /// seconds since; the expiry is that plus the quest's remaining seconds. Before any GameTime, the same UTC
        /// seconds the next one will carry.
        /// </summary>
        static uint ClientExpiry(Player player, DateTime expiresUtc, DateTime nowUtc)
        {
            if (expiresUtc <= nowUtc)
                return 0;

            long clientNow = player.Session is Network.IGameTimeSession { GameTimeSynchronizedAtUtc: DateTime at } clock
                ? clock.GameTimeServerSeconds + (long)Math.Max(0, (nowUtc - at).TotalSeconds)
                : Network.GameClock.UnixSeconds(nowUtc);
            long expires = clientNow + (long)Math.Ceiling((expiresUtc - nowUtc).TotalSeconds);
            return (uint)Math.Min(expires, int.MaxValue);
        }

        /// <summary>
        /// Quest action versions whose live action names its target (captured journals): 1 kill one, 6 deliver/return/show,
        /// 8 use on an object. Talk (24), assemble (18) and kill in area (20) send only a point.
        /// </summary>
        static readonly HashSet<int> TargetedActionVersions = [1, 6, 8];

        /// <summary>
        /// The point a Quests.json waypoint sends: its target's position when that resolves, else its
        /// <see cref="QuestWaypoint.Position"/>; null when it has neither. The target is <see cref="QuestWaypoint.Target"/>,
        /// or for an action live names a target on, the step's objective NPC.
        /// </summary>
        static SmokeLounge.AOtomation.Messaging.GameData.Vector3? WaypointPoint(Player player, QuestWaypoint waypoint,
            QuestTemplate template, int actionVersion)
        {
            string? target = !string.IsNullOrEmpty(waypoint.Target) ? waypoint.Target
                : TargetedActionVersions.Contains(actionVersion) ? template.Objective.Npc : null;
            if (!string.IsNullOrEmpty(target) && TryTargetPosition(player, waypoint.Playfield, target, out float x, out float z))
                // Live sends whole-number x and z, and y as 0 (Alex Gibbs at 3520.79, 856.69 is sent as 3520, 856).
                return new SmokeLounge.AOtomation.Messaging.GameData.Vector3(MathF.Truncate(x), 0, MathF.Truncate(z));

            return waypoint.Position is { Length: 3 } position
                ? new SmokeLounge.AOtomation.Messaging.GameData.Vector3(position[0], position[1], position[2])
                : null;
        }

        /// <summary>
        /// Where <paramref name="target"/> is on <paramref name="playfieldId"/>, nearest the player when there are several.
        /// On the player's playfield the spawn system answers: a living NPC of that spawn or template hash, else its spawn
        /// point. Otherwise, or when it has none, the hash's Spawns.json records and the Dynels.dat statels whose template
        /// the ItemTemplates.json hash covers.
        /// </summary>
        static bool TryTargetPosition(Player player, int playfieldId, string target, out float x, out float z)
        {
            x = z = 0;
            if (player.Playfield is not { } playfield || playfield.GetService<IGameData>() is not IGameData gameData)
                return false;

            AORebirth.Core.Vector.Vector3 from = player.Position;
            if (playfield.Identity.Instance == playfieldId && playfield.GetService<HashSpawnSystem>() is HashSpawnSystem spawns
                && spawns.TryFindNearest(target, from, out AORebirth.Core.Vector.Vector3 at))
            {
                (x, z) = ((float)at.x, (float)at.z);
                return true;
            }

            float[] points = StaticTargets.GetOrAdd((playfieldId, target), key => BuildStaticTargets(gameData, key.Playfield, key.Hash));
            double best = double.MaxValue;
            for (int i = 0; i + 1 < points.Length; i += 2)
            {
                double dx = points[i] - from.x, dz = points[i + 1] - from.z, distance = (dx * dx) + (dz * dz);
                if (distance < best)
                    (best, x, z) = (distance, points[i], points[i + 1]);
            }

            return best < double.MaxValue;
        }

        static float[] BuildStaticTargets(IGameData gameData, int playfieldId, string target)
        {
            var points = new List<float>();
            foreach (PlayfieldSpawnEntry spawn in gameData.GetPlayfieldSpawns(playfieldId).Spawns ?? [])
            {
                if (string.Equals(spawn.HashText, target, StringComparison.Ordinal) && spawn.Position is { Length: >= 3 } at)
                {
                    points.Add(at[0]);
                    points.Add(at[2]);
                }
            }

            if (points.Count > 0)
                return points.ToArray();

            var leaves = new List<HashInstance>();
            gameData.CollectHashLeafInstances(target, leaves);
            if (leaves.Count > 0 && gameData.GetPlayfieldGeometry(playfieldId).Dynels?.Dynels is { } statels)
            {
                var templates = new HashSet<int>();
                foreach (HashInstance leaf in leaves)
                    templates.UnionWith(leaf.TemplateIds);
                foreach (var statel in statels)
                {
                    if (templates.Contains(statel.TemplateId))
                    {
                        points.Add(statel.Position.X);
                        points.Add(statel.Position.Z);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>Retail cuts a longer title to 28 characters and "..." ("Talk to the ICC Bio-Inspecto...", HYIY).</summary>
        static string ShortName(string name)
            => name.Length <= MaxShortInfoLength ? name : name[..(MaxShortInfoLength - 3)] + "...";

        static string LongInfo(QuestTemplate template)
        {
            if (!string.IsNullOrWhiteSpace(template.Description))
                return template.Description;

            var text = new StringBuilder(template.Name);
            if (!string.IsNullOrEmpty(template.Summary))
            {
                text.Append("<BR><BR>").Append(Capitalize(template.Summary));
                if (!template.Summary.EndsWith('.') && !template.Summary.EndsWith('!') && !template.Summary.EndsWith('?'))
                    text.Append('.');
            }
            return text.ToString();
        }

        /// <summary>The reward items the journal shows: what completing the quest would pay this player now.</summary>
        static MissionItemReward[] RewardItems(Player player, PlayerQuest quest)
        {
            IReadOnlyList<QuestRewardItem> shown;
            try
            {
                shown = player.Playfield?.GetService<QuestService>()?.RewardItemsFor(player, quest) ?? [];
            }
            catch (InvalidOperationException)
            {
                // A mapping that no longer resolves shows no items; completion reports it.
                shown = [];
            }

            var items = new List<MissionItemReward>(shown.Count);
            foreach (QuestRewardItem item in shown)
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
