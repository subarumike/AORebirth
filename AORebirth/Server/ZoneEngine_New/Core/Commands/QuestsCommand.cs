namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility.GameData.Missions;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Quests;
    using ZoneEngine_New.Core.Quests.Dungeons;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// <c>.quests</c>: the selected player's (or your own) active quests in a popup. Every quest gets a link to
    /// <c>.quests info &lt;questId&gt; &lt;characterId&gt;</c> (all of its details), and a quest with an ACG dungeon a link
    /// that runs <c>.quests tp &lt;questId&gt;</c>, which puts you (never the selected player) at its entrance.
    /// </summary>
    public sealed class QuestsCommand : IGmCommand
    {
        readonly Lazy<PlayfieldManager> _playfields;
        readonly QuestService _quests;
        readonly QuestDungeonService _dungeons;
        readonly QuestCatalog _catalog;

        public QuestsCommand(Lazy<PlayfieldManager> playfields, QuestService quests, QuestDungeonService dungeons, QuestCatalog catalog)
        {
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _dungeons = dungeons ?? throw new ArgumentNullException(nameof(dungeons));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public string Name => "quests";

        public int RequiredGmLevel => 1;

        public string Usage => ".quests (selected player's active quests) | .quests info <questId> [characterId] | .quests tp <questId> | .quests done [characterId] | .quests wipe <questHash> [characterId]";

        public GmCommandHelp Help { get; } = new(
            "Shows and inspects quests.",
            [
                new(".quests", "Your look-at player's active quests (or yours)."),
                new(".quests info <questId> [characterId]", "Every detail of one quest, in a popup."),
                new(".quests tp <questId>", "Takes you to the quest's dungeon entrance."),
                new(".quests done [characterId]", "Your look-at player's completed quests: every quest whose mission bit is set, plus completed journal entries."),
                new(".quests wipe <questHash> [characterId]", "Development: clears the quest's mission bit on your look-at player, so it counts as not completed."),
            ],
            [
                new(".quests", "Active quests of your target."),
                new(".quests info 12", "Details of quest 12."),
                new(".quests tp 12", "Go to quest 12's entrance."),
                new(".quests done", "Completed quests of your target, each with a wipe link."),
                new(".quests wipe ABCD", "Clear quest ABCD's mission bit on your target."),
            ],
            "Wipe only clears the mission bit (what HasCompletedQuest and item requirements read). A completed journal entry "
            + "for the quest stays, so a 'quest HASH done' check still passes. Quests that share the bit are cleared with it.");

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length >= 1 && string.Equals(context.Args[0], "tp", StringComparison.OrdinalIgnoreCase))
            {
                TeleportToEntrance(context);
                return;
            }

            if (context.Args.Length >= 1 && string.Equals(context.Args[0], "done", StringComparison.OrdinalIgnoreCase))
            {
                ShowCompleted(context);
                return;
            }

            if (context.Args.Length >= 1 && string.Equals(context.Args[0], "wipe", StringComparison.OrdinalIgnoreCase))
            {
                WipeMissionBit(context);
                return;
            }

            if (context.Args.Length >= 1 && string.Equals(context.Args[0], "info", StringComparison.OrdinalIgnoreCase))
            {
                ShowInfo(context);
                return;
            }

            if (!context.TryResolveSubject(out Player subject, requirePlayerTarget: true))
                return;

            DateTime now = DateTime.UtcNow;
            var rows = new List<string>();
            foreach (PlayerQuest quest in _quests.GetLog(subject).Quests.Values)
            {
                if (!quest.IsActive)
                    continue;

                string row = string.Format(CultureInfo.InvariantCulture, "{0} [{1}] {2}  {3}",
                    Safe(quest.Template.Name), Safe(quest.QuestId), Remaining(quest, now), InfoLink(quest.QuestId, subject));
                if (_dungeons.TryGetEntrance(quest.QuestId, out MissionEntrancePlacement entrance))
                    row += string.Format(CultureInfo.InvariantCulture, "  <a href='chatcmd:///say .quests tp {0}'>[TP: {1}]</a>",
                        quest.QuestId, Safe(entrance.DisplayName));
                rows.Add(row);
            }

            string who = string.IsNullOrEmpty(subject.Name) ? subject.Identity.Instance.ToString(CultureInfo.InvariantCulture) : subject.Name;
            if (rows.Count == 0)
            {
                GmCommandFeedback.Send(context.Session, context.Player, who + " has no active quests.");
                return;
            }

            string title = string.Format(CultureInfo.InvariantCulture, "{0} quests ({1})", who, rows.Count);
            IReadOnlyList<string> chunks = GetStatsAomlBuilder.ChunkRows(rows, GetStatsAomlBuilder.DefaultMaxBodyLength);
            var lines = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                string label = chunks.Count == 1
                    ? title
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, i + 1, chunks.Count);
                lines.Add(GetStatsAomlBuilder.BuildLink(chunks[i], label));
            }

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }

        static string InfoLink(string questId, Player subject)
            => string.Format(CultureInfo.InvariantCulture, "<a href='chatcmd:///say .quests info {0} {1}'>[Info]</a>",
                Safe(questId), subject.Identity.Instance);

        /// <summary>
        /// Every detail of one quest in a popup: the quest itself, its objective and reward, and for a dungeon quest the
        /// stored dungeon parameters, whether the dungeon is loaded, who is in it and the keys the owner carries.
        /// </summary>
        void ShowInfo(GmCommandContext context)
        {
            if (context.Args.Length < 2)
            {
                context.SendUsage("Usage: .quests info <questId> [characterId]");
                return;
            }

            string questId = context.Args[1];
            Player subject;
            if (context.Args.Length >= 3)
            {
                if (!int.TryParse(context.Args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int characterId)
                    || !_playfields.Value.FindPlayer(characterId, out subject))
                {
                    GmCommandFeedback.Send(context.Session, context.Player, "That character is not online.");
                    return;
                }
            }
            else if (!context.TryResolveSubject(out subject, requirePlayerTarget: true))
                return;

            string who = string.IsNullOrEmpty(subject.Name) ? subject.Identity.Instance.ToString(CultureInfo.InvariantCulture) : subject.Name;
            if (!_quests.GetLog(subject).Quests.TryGetValue(questId, out PlayerQuest? quest))
            {
                GmCommandFeedback.Send(context.Session, context.Player, who + " has no quest " + Safe(questId) + ".");
                return;
            }

            DateTime now = DateTime.UtcNow;
            QuestTemplate template = quest.Template;
            var rows = new List<string>
            {
                Row("Quest", Safe(template.Name) + " [" + Safe(quest.QuestId) + "]"),
                Row("Owner", Safe(who) + " (" + subject.Identity.Instance.ToString(CultureInfo.InvariantCulture) + ")"),
                Row("Source", quest.Source.ToString()),
                Row("State", quest.State.ToString()),
                Row("Action", Safe(template.Action) + ", scope " + Safe(template.Scope) + ", flags " + Safe(template.Flags)),
                Row("Progress", quest.Progress.ToString(CultureInfo.InvariantCulture) + " / " + quest.RequiredCount.ToString(CultureInfo.InvariantCulture)),
                Row("Assigned", Utc(quest.AssignedAtUtc)),
                Row("Expires", quest.ExpiresAtUtc is DateTime expires ? Utc(expires) + " " + Remaining(quest, now) : "never")
            };

            if (!string.IsNullOrWhiteSpace(template.Hash))
                rows.Add(Row("Template", Safe(template.Hash) + (template.QasInstance != 0 ? ", QAS " + template.QasInstance.ToString(CultureInfo.InvariantCulture) : string.Empty)));
            if (template.MissionBit is int bit)
                rows.Add(Row("Mission bit", bit.ToString(CultureInfo.InvariantCulture)));
            if (!string.IsNullOrWhiteSpace(template.NextStep))
                rows.Add(Row("Next step", Safe(template.NextStep)));
            if (!string.IsNullOrWhiteSpace(template.Summary))
                rows.Add(Row("Summary", Safe(template.Summary)));

            QuestObjective objective = template.Objective;
            var objectiveParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(objective.Npc)) objectiveParts.Add("NPC " + Safe(objective.Npc));
            if (objective.Count is int count) objectiveParts.Add("count " + count.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(objective.SpecItem)) objectiveParts.Add("item " + Safe(objective.SpecItem));
            if (!string.IsNullOrWhiteSpace(objective.WorldItem)) objectiveParts.Add("world item " + Safe(objective.WorldItem));
            rows.Add(Row("Objective", objectiveParts.Count == 0 ? "none" : string.Join(", ", objectiveParts)));

            if (template.Reward is QuestReward reward)
            {
                rows.Add(Row("Reward", reward.Credits.ToString(CultureInfo.InvariantCulture) + " credits, "
                    + reward.Xp.ToString(CultureInfo.InvariantCulture) + " XP" + (string.IsNullOrWhiteSpace(reward.Hash) ? string.Empty : " (" + Safe(reward.Hash) + ")")));
                if (reward.Records != null)
                {
                    for (int i = 0; i < reward.Records.Count; i++)
                    {
                        var items = new List<string>();
                        foreach (QuestRewardItem item in reward.Records[i])
                            items.Add(Safe(string.IsNullOrEmpty(item.Hash) ? item.Name : item.Hash) + " QL" + item.Quality.ToString(CultureInfo.InvariantCulture) + " (" + item.Id.ToString(CultureInfo.InvariantCulture) + ")");
                        rows.Add(Row("Reward choice " + (i + 1).ToString(CultureInfo.InvariantCulture), string.Join(", ", items)));
                    }
                }
            }
            else
                rows.Add(Row("Reward", "none"));

            if (_dungeons.TryGetParameters(quest.QuestId, out QuestDungeonParameters dungeon))
            {
                rows.Add(Row("Dungeon", "playfield " + dungeon.DungeonPlayfield.ToString(CultureInfo.InvariantCulture)
                    + ", seed " + dungeon.Seed.ToString(CultureInfo.InvariantCulture)
                    + ", generator v" + dungeon.GeneratorVersion.ToString(CultureInfo.InvariantCulture)));
                rows.Add(Row("Mission", "type " + dungeon.MissionType.ToString(CultureInfo.InvariantCulture)
                    + ", icon " + dungeon.MissionIconId.ToString(CultureInfo.InvariantCulture)));
                rows.Add(Row("Entrance", Safe(dungeon.EntranceName) + " (instance " + dungeon.EntranceInstance.ToString(CultureInfo.InvariantCulture)
                    + ") on pf " + dungeon.EntrancePlayfield.ToString(CultureInfo.InvariantCulture)
                    + string.Format(CultureInfo.InvariantCulture, " at ({0:0.#}, {1:0.#}, {2:0.#})", dungeon.EntranceX, dungeon.EntranceY, dungeon.EntranceZ)
                    + string.Format(CultureInfo.InvariantCulture, "  <a href='chatcmd:///say .quests tp {0}'>[TP]</a>", Safe(quest.QuestId))));
                rows.Add(Row("Building", "type " + dungeon.DestinationType.ToString(CultureInfo.InvariantCulture)
                    + ", ids " + dungeon.BuildingLowId.ToString(CultureInfo.InvariantCulture) + "/" + dungeon.BuildingHighId.ToString(CultureInfo.InvariantCulture)));

                if (_playfields.Value.TryGetQuestDungeon(dungeon.DungeonPlayfield, out QuestDungeonPlayfield loaded))
                {
                    var inside = new List<string>();
                    foreach (Player player in loaded.GetRequiredService<DynelRegistry>().PlayerEntities())
                        inside.Add(Safe(player.Name));
                    rows.Add(Row("Loaded", "yes, style " + loaded.Layout.Generator.Style.ToString(CultureInfo.InvariantCulture)
                        + ", " + (loaded.Layout.Generator.Rooms?.Length ?? 0).ToString(CultureInfo.InvariantCulture) + " rooms, "
                        + (inside.Count == 0 ? "empty" : "inside: " + string.Join(", ", inside))));
                }
                else
                    rows.Add(Row("Loaded", "no (built when someone enters)"));

                rows.Add(Row("Keys carried", _dungeons.CountKeysFor(subject, quest.QuestId).ToString(CultureInfo.InvariantCulture)));
            }

            string title = string.Format(CultureInfo.InvariantCulture, "{0} {1}", Safe(template.Name), Safe(quest.QuestId));
            IReadOnlyList<string> chunks = GetStatsAomlBuilder.ChunkRows(rows, GetStatsAomlBuilder.DefaultMaxBodyLength);
            var lines = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                string label = chunks.Count == 1
                    ? title
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, i + 1, chunks.Count);
                lines.Add(GetStatsAomlBuilder.BuildLink(chunks[i], label));
            }

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }

        /// <summary>
        /// The subject for <c>done</c> / <c>wipe</c>: an online character id at <paramref name="index"/>, else the look-at
        /// player, else the GM.
        /// </summary>
        bool TryResolveSubject(GmCommandContext context, int index, out Player subject)
        {
            if (context.Args.Length > index)
            {
                if (!int.TryParse(context.Args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int characterId)
                    || !_playfields.Value.FindPlayer(characterId, out subject))
                {
                    GmCommandFeedback.SendError(context.Session, context.Player, "That character is not online.");
                    subject = null!;
                    return false;
                }

                return true;
            }

            return context.TryResolveSubject(out subject, requirePlayerTarget: true);
        }

        static string WhoOf(Player subject)
            => string.IsNullOrEmpty(subject.Name) ? subject.Identity.Instance.ToString(CultureInfo.InvariantCulture) : Safe(subject.Name);

        /// <summary>
        /// Completed quests: every catalog quest whose mission bit the subject has (by bit order), then completed journal
        /// entries whose quest sets no bit. Each bit row links to <c>.quests wipe</c>.
        /// </summary>
        void ShowCompleted(GmCommandContext context)
        {
            if (!TryResolveSubject(context, 1, out Player subject))
                return;

            int characterId = subject.Identity.Instance;
            var byBit = new SortedDictionary<int, List<QuestTemplate>>();
            foreach (QuestTemplate template in _catalog.Templates)
            {
                if (template.MissionBit is not int bit || !MissionBits.Has(subject.Stats, bit))
                    continue;
                if (!byBit.TryGetValue(bit, out List<QuestTemplate>? list))
                    byBit[bit] = list = new List<QuestTemplate>();
                list.Add(template);
            }

            var journalOnly = new List<PlayerQuest>();
            foreach (PlayerQuest quest in _quests.GetLog(subject).Quests.Values)
            {
                if (quest.State != QuestState.Completed)
                    continue;
                if (quest.Template.MissionBit is int bit && byBit.ContainsKey(bit))
                    continue;
                journalOnly.Add(quest);
            }

            string who = WhoOf(subject);
            int total = 0;
            foreach (List<QuestTemplate> list in byBit.Values)
                total += list.Count;
            total += journalOnly.Count;
            if (total == 0)
            {
                GmCommandFeedback.Send(context.Session, context.Player, who + " has no completed quests.");
                return;
            }

            var rows = new List<string>
            {
                DiagAoml.Title(who + " - completed quests"),
                DiagAoml.Muted(string.Format(CultureInfo.InvariantCulture, "{0} by mission bit, {1} from the journal only. [Wipe] clears the bit.",
                    total - journalOnly.Count, journalOnly.Count)),
            };

            if (byBit.Count > 0)
            {
                rows.Add(DiagAoml.Section("Mission bits"));
                foreach (KeyValuePair<int, List<QuestTemplate>> pair in byBit)
                {
                    foreach (QuestTemplate template in pair.Value)
                    {
                        rows.Add(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} {1} {2} {3}  <a href='chatcmd:///say .quests wipe {4} {5}'>{6}</a>",
                            DiagAoml.Color(DiagAoml.Green, "+"),
                            DiagAoml.Name(Safe(template.Name)),
                            DiagAoml.Label("[" + Safe(template.Hash) + "]"),
                            DiagAoml.Muted("bit " + pair.Key.ToString(CultureInfo.InvariantCulture)
                                + (pair.Value.Count > 1 ? ", shared" : string.Empty)),
                            Safe(template.Hash),
                            characterId,
                            DiagAoml.Color(DiagAoml.Red, "[Wipe]")));
                    }
                }
            }

            if (journalOnly.Count > 0)
            {
                rows.Add(DiagAoml.Section("Journal only (no mission bit)"));
                foreach (PlayerQuest quest in journalOnly)
                {
                    rows.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} {1} {2} {3}",
                        DiagAoml.Color(DiagAoml.Yellow, "*"),
                        DiagAoml.Name(Safe(quest.Template.Name)),
                        DiagAoml.Label("[" + Safe(string.IsNullOrWhiteSpace(quest.Template.Hash) ? quest.QuestId : quest.Template.Hash) + "]"),
                        DiagAoml.Muted("assigned " + Utc(quest.AssignedAtUtc))));
                }
            }

            string title = string.Format(CultureInfo.InvariantCulture, "{0} completed quests ({1})", who, total);
            IReadOnlyList<string> chunks = GetStatsAomlBuilder.ChunkRows(rows, GetStatsAomlBuilder.DefaultMaxBodyLength);
            var lines = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                string label = chunks.Count == 1
                    ? title
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, i + 1, chunks.Count);
                lines.Add(GetStatsAomlBuilder.BuildLink(chunks[i], label));
            }

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }

        /// <summary>Development: clears the quest's mission bit on the subject and reports every quest that shares it.</summary>
        void WipeMissionBit(GmCommandContext context)
        {
            if (context.Args.Length < 2)
            {
                context.SendUsage("Usage: .quests wipe <questHash> [characterId]");
                return;
            }

            string hash = context.Args[1].Trim();
            if (!_catalog.TryGet(hash, out QuestTemplate template)
                && !_catalog.TryGet(hash.ToUpperInvariant(), out template))
            {
                GmCommandFeedback.SendError(context.Session, context.Player, "No quest with hash " + Safe(hash) + " in Quests.json.");
                return;
            }

            if (template.MissionBit is not int bit)
            {
                GmCommandFeedback.SendError(context.Session, context.Player,
                    Safe(template.Name) + " [" + Safe(template.Hash) + "] sets no mission bit; nothing to wipe.");
                return;
            }

            if (!TryResolveSubject(context, 2, out Player subject))
                return;

            string who = WhoOf(subject);
            var shared = new List<string>();
            foreach (QuestTemplate other in _catalog.Templates)
            {
                if (other.MissionBit == bit && !ReferenceEquals(other, template))
                    shared.Add(Safe(other.Name) + " [" + Safe(other.Hash) + "]");
            }

            bool wasSet = MissionBits.Has(subject.Stats, bit);
            (CharacterStat stat, int mask) = MissionBits.Locate(bit);
            bool baseSet = (subject.Stats.GetOrZero(stat, StatDetail.Base) & mask) != 0;
            if (baseSet)
            {
                MissionBits.Clear(subject.Stats, bit);
                subject.FlushDirtyStats();
            }

            var lines = new List<string>();
            string quest = DiagAoml.Name(Safe(template.Name)) + " " + DiagAoml.Label("[" + Safe(template.Hash) + "]");
            string bitText = DiagAoml.Muted("bit " + bit.ToString(CultureInfo.InvariantCulture) + " (" + stat + ")");
            if (baseSet)
                lines.Add(DiagAoml.Color(DiagAoml.Green, "Wiped") + " " + quest + " " + bitText + " on " + DiagAoml.Name(who) + ".");
            else if (wasSet)
                lines.Add(DiagAoml.Color(DiagAoml.Orange, "Not stored") + " " + quest + " " + bitText + " on " + DiagAoml.Name(who)
                    + ": the bit comes from worn gear, not the character, so it cannot be wiped here.");
            else
                lines.Add(DiagAoml.Color(DiagAoml.Yellow, "Already clear") + " " + quest + " " + bitText + " on " + DiagAoml.Name(who) + ".");

            if (shared.Count > 0)
                lines.Add(DiagAoml.Color(DiagAoml.Orange, "Shared bit:") + " " + DiagAoml.Muted(string.Join(", ", shared)));

            bool journalDone = false;
            foreach (PlayerQuest entry in _quests.GetLog(subject).Quests.Values)
            {
                if (entry.State == QuestState.Completed && string.Equals(entry.Template.Hash, template.Hash, StringComparison.Ordinal))
                {
                    journalDone = true;
                    break;
                }
            }

            if (journalDone)
                lines.Add(DiagAoml.Color(DiagAoml.Orange, "Journal:") + " " + DiagAoml.Muted("a completed journal entry remains, so 'quest " + Safe(template.Hash) + " done' still passes."));

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }

        static string Row(string label, string value) => label + ": " + value;

        static string Utc(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

        /// <summary>Moves the command's issuer to the quest dungeon's entrance, resolved from the quest on the server.</summary>
        void TeleportToEntrance(GmCommandContext context)
        {
            Player self = context.Player;
            if (context.Args.Length < 2 || !QuestDungeonIds.TryGetPlayfield(context.Args[1], out _)
                || !_dungeons.TryGetEntrance(context.Args[1], out MissionEntrancePlacement entrance))
            {
                GmCommandFeedback.Send(context.Session, self, "No dungeon entrance for that quest.");
                return;
            }

            if (self.Playfield is not Playfield playfield || self.Session == null)
            {
                GmCommandFeedback.Send(context.Session, self, "Not on a playfield.");
                return;
            }

            var landing = new Vector3(entrance.LocalX, entrance.LocalY, entrance.LocalZ);
            GmCommandFeedback.Send(context.Session, self, string.Format(CultureInfo.InvariantCulture,
                "Teleported to {0} ({1}, {2}, {3}) pf={4}", entrance.DisplayName, entrance.LocalX, entrance.LocalY, entrance.LocalZ, entrance.PlayfieldId));

            if (playfield.Identity.Instance != entrance.PlayfieldId)
            {
                // A GM jump is not a proxy entry: it leaves no way back through an exit proxy.
                self.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
                self.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
                _playfields.Value.WithPlayfield(entrance.PlayfieldId, self, destination =>
                {
                    if (ReferenceEquals(self.Playfield, playfield))
                        self.Session?.TransferToPlayfield(destination, landing);
                }, new ZoneEngine_New.Core.World.RemoteArrival(landing, Kind: AORebirth.World.Contracts.V1.ArrivalKind.ArrivalTeleport));
                return;
            }

            self.Position = landing;
            playfield.GetRequiredService<PlayfieldLocality>().Announce(self, new CharDCMoveMessage
            {
                Identity = self.Identity,
                Unknown = 0x00,
                MoveType = (byte)MovementAction.FullStop,
                Heading = new MsgQuaternion { X = self.Rotation.xf, Y = self.Rotation.yf, Z = self.Rotation.zf, W = self.Rotation.wf },
                Coordinates = new MsgVector3 { X = self.Position.xf, Y = self.Position.yf, Z = self.Position.zf },
                Unknown1 = 0,
                AuxA = 0,
                AuxB = 0
            }, includeSelf: true);
        }

        static string Remaining(PlayerQuest quest, DateTime now)
        {
            if (quest.ExpiresAtUtc is not DateTime expires)
                return string.Empty;

            TimeSpan left = expires - now;
            return left <= TimeSpan.Zero
                ? "(expired)"
                : string.Format(CultureInfo.InvariantCulture, "({0}:{1:00} left)", (int)left.TotalHours, left.Minutes);
        }

        /// <summary>
        /// Keeps text from breaking out of the popup's AOML. Control characters go too: stored mission text can carry
        /// its wire NUL, and the client ends a chat string at the first NUL, cutting the link off mid-popup.
        /// </summary>
        static string Safe(string? text)
        {
            string value = (text ?? string.Empty).Replace('"', '\'').Replace("'", "`", StringComparison.Ordinal).Replace('<', '(').Replace('>', ')');
            var clean = new System.Text.StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c == '\0')
                    continue;
                clean.Append(char.IsControl(c) ? ' ' : c);
            }

            return clean.ToString().Trim();
        }
    }
}
