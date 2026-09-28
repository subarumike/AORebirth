namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

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

        public QuestsCommand(Lazy<PlayfieldManager> playfields, QuestService quests, QuestDungeonService dungeons)
        {
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _dungeons = dungeons ?? throw new ArgumentNullException(nameof(dungeons));
        }

        public string Name => "quests";

        public int RequiredGmLevel => 1;

        public string Usage => ".quests (selected player's active quests) | .quests info <questId> [characterId] (all of a quest's details) | .quests tp <questId> (you, to its dungeon entrance)";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length >= 1 && string.Equals(context.Args[0], "tp", StringComparison.OrdinalIgnoreCase))
            {
                TeleportToEntrance(context);
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
                if (_dungeons.TryGetEntrance(quest.QuestId, out MissionEntrance entrance))
                    row += string.Format(CultureInfo.InvariantCulture, "  <a href='chatcmd:///say .quests tp {0}'>[TP: {1}]</a>",
                        quest.QuestId, Safe(entrance.Name));
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
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: .quests info <questId> [characterId]");
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
                            items.Add(Safe(item.Name) + " QL" + item.Quality.ToString(CultureInfo.InvariantCulture) + " (" + item.Id.ToString(CultureInfo.InvariantCulture) + ")");
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

        static string Row(string label, string value) => label + ": " + value;

        static string Utc(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

        /// <summary>Moves the command's issuer to the quest dungeon's entrance, resolved from the quest on the server.</summary>
        void TeleportToEntrance(GmCommandContext context)
        {
            Player self = context.Player;
            if (context.Args.Length < 2 || !QuestDungeonIds.TryGetPlayfield(context.Args[1], out _)
                || !_dungeons.TryGetEntrance(context.Args[1], out MissionEntrance entrance))
            {
                GmCommandFeedback.Send(context.Session, self, "No dungeon entrance for that quest.");
                return;
            }

            if (self.Playfield is not Playfield playfield || self.Session == null)
            {
                GmCommandFeedback.Send(context.Session, self, "Not on a playfield.");
                return;
            }

            var landing = new Vector3(entrance.X, entrance.Y, entrance.Z);
            GmCommandFeedback.Send(context.Session, self, string.Format(CultureInfo.InvariantCulture,
                "Teleported to {0} ({1}, {2}, {3}) pf={4}", entrance.Name, entrance.X, entrance.Y, entrance.Z, entrance.Playfield));

            if (playfield.Identity.Instance != entrance.Playfield)
            {
                // A GM jump is not a proxy entry: it leaves no way back through an exit proxy.
                self.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
                self.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
                self.Session.TransferToPlayfield(_playfields.Value.GetOrCreate(entrance.Playfield), landing);
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
