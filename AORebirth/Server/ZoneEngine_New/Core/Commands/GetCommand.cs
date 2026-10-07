namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Quests;

    public sealed class GetCommand : IGmCommand
    {
        readonly QuestService? _quests;

        public GetCommand()
        {
        }

        public GetCommand(QuestService quests)
        {
            _quests = quests;
        }

        public string Name => "get";

        public int RequiredGmLevel => 1;

        public string Usage => ".get stat <statName|statId> | .get stats | .get buffs | .get quests | .get weapons";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 1)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            string verb = context.Args[0];
            if (string.Equals(verb, "stat", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteStat(context);
                return;
            }

            if (string.Equals(verb, "stats", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteStats(context);
                return;
            }

            if (string.Equals(verb, "buffs", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteBuffs(context);
                return;
            }

            if (string.Equals(verb, "quests", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteQuests(context);
                return;
            }

            if (string.Equals(verb, "weapons", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteWeapons(context);
                return;
            }

            GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
        }

        static void ExecuteStat(GmCommandContext context)
        {
            if (context.Args.Length < 2 || !CharacterStatParser.TryParse(context.Args[1], out CharacterStat stat))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: .get stat <statName|statId>");
                return;
            }

            if (!context.TryResolveCharacter(out Character subject))
                return;

            if (!subject.Stats.TryGetValue(stat, out _))
            {
                GmCommandFeedback.Send(
                    context.Session,
                    context.Player,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} ({1}) = <unset> [{2}]",
                        stat,
                        (int)stat,
                        subject.Name ?? string.Empty));
                return;
            }

            int full = subject.Stats.Get(stat, StatDetail.Full);
            int bas = subject.Stats.Get(stat, StatDetail.Base);
            int bonus = subject.Stats.Get(stat, StatDetail.Bonus);

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} ({1}) = {2} (base={3} bonus={4}) [{5}]",
                    stat,
                    (int)stat,
                    full,
                    bas,
                    bonus,
                    subject.Name ?? string.Empty));
        }

        static void ExecuteStats(GmCommandContext context)
        {
            if (!context.TryResolveCharacter(out Character subject))
                return;

            IReadOnlyList<string> lines = GetStatsAomlBuilder.BuildChatLines(
                subject.Name ?? string.Empty,
                subject.Stats,
                Player.FullCharacterStatSets,
                Player.FullCharacterStatSetNames);

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }

        /// <summary>The targeted player's (or your own) quests with progress, state and expiry.</summary>
        void ExecuteQuests(GmCommandContext context)
        {
            if (_quests == null)
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Quests are not available.");
                return;
            }

            Player subject = context.ResolveSubject();
            QuestLog log = _quests.GetLog(subject);
            var lines = new List<string>
            {
                string.Format(CultureInfo.InvariantCulture, "{0} quests: {1}", subject.Name ?? string.Empty, log.Quests.Count)
            };

            foreach (PlayerQuest quest in log.Quests.Values)
            {
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0} {1} [{2} {3}] {4} {5}/{6}{7}",
                    quest.QuestId,
                    quest.Template.Name,
                    quest.Source,
                    quest.Template.Action,
                    quest.State,
                    quest.Progress,
                    quest.RequiredCount,
                    quest.ExpiresAtUtc is DateTime expires
                        ? string.Format(CultureInfo.InvariantCulture, " expires {0:u}", expires)
                        : string.Empty));
            }

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }

        /// <summary>The target's armed weapons with their item template damage stats.</summary>
        static void ExecuteWeapons(GmCommandContext context)
        {
            if (!context.TryResolveCharacter(out Character subject))
                return;

            GmCommandFeedback.SendLines(
                context.Session,
                context.Player,
                GetWeaponsAomlBuilder.BuildChatLines(subject.Name ?? string.Empty, subject.Weapons));
        }

        static void ExecuteBuffs(GmCommandContext context)
        {
            if (!context.TryResolveCharacter(out Character subject))
                return;

            IReadOnlyList<string> lines = GetBuffsAomlBuilder.BuildChatLines(
                subject.Name ?? string.Empty,
                subject.Buffs,
                subject.UsedNcu,
                subject.MaxNcu,
                DateTime.UtcNow);

            GmCommandFeedback.SendLines(context.Session, context.Player, lines);
        }
    }

    /// <summary>Builds AOML <c>text://</c> popup links for FullCharacter stat sets.</summary>
    internal static class GetStatsAomlBuilder
    {
        /// <summary>Popup body length before a GM listing is split into further links.</summary>
        public const int DefaultMaxBodyLength = 4096;

        public static IReadOnlyList<string> BuildChatLines(
            string subjectName,
            StatCollection stats,
            IReadOnlyList<CharacterStat[]> sets,
            IReadOnlyList<string> setNames,
            int maxBodyLength = DefaultMaxBodyLength)
        {
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentNullException.ThrowIfNull(sets);
            ArgumentNullException.ThrowIfNull(setNames);
            if (maxBodyLength < 64)
                throw new ArgumentOutOfRangeException(nameof(maxBodyLength));

            string title = string.IsNullOrWhiteSpace(subjectName) ? "Stats" : subjectName + " Stats";
            var lines = new List<string>();

            for (int setIndex = 0; setIndex < sets.Count; setIndex++)
            {
                CharacterStat[] set = sets[setIndex];
                if (set == null || set.Length == 0)
                    continue;

                IReadOnlyList<string> rows = BuildRows(stats, set);
                IReadOnlyList<string> chunks = ChunkRows(rows, maxBodyLength);
                string setName = ResolveSetName(setNames, setIndex);

                for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                {
                    string label = chunks.Count == 1
                        ? string.Format(CultureInfo.InvariantCulture, "{0} — {1}", title, setName)
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} — {1} ({2}/{3})",
                            title,
                            setName,
                            chunkIndex + 1,
                            chunks.Count);

                    lines.Add(BuildLink(chunks[chunkIndex], label));
                }
            }

            if (lines.Count == 0)
                lines.Add(BuildLink("(no stats)", title));

            return lines;
        }

        static string ResolveSetName(IReadOnlyList<string> setNames, int setIndex)
        {
            if (setIndex < setNames.Count)
            {
                string name = setNames[setIndex];
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            return string.Format(CultureInfo.InvariantCulture, "Set {0}", setIndex + 1);
        }

        public static string BuildLink(string body, string label)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(label);
            return string.Format(
                CultureInfo.InvariantCulture,
                "<a href=\"text://{0}\">{1}</a>",
                body,
                label);
        }

        public static IReadOnlyList<string> BuildRows(StatCollection stats, CharacterStat[] set)
        {
            ArgumentNullException.ThrowIfNull(stats);
            ArgumentNullException.ThrowIfNull(set);

            var rows = new List<string>(set.Length);
            for (int i = 0; i < set.Length; i++)
            {
                CharacterStat stat = set[i];
                string valueText;
                if (!stats.TryGetValue(stat, out _))
                    valueText = "<unset>";
                else
                {
                    int full = stats.Get(stat, StatDetail.Full);
                    int bas = stats.Get(stat, StatDetail.Base);
                    int bonus = stats.Get(stat, StatDetail.Bonus);
                    valueText = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} (b={1} +{2})",
                        full,
                        bas,
                        bonus);
                }

                rows.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} ({1}) = {2}",
                        stat,
                        (int)stat,
                        valueText));
            }

            return rows;
        }

        public static IReadOnlyList<string> ChunkRows(IReadOnlyList<string> rows, int maxBodyLength)
        {
            ArgumentNullException.ThrowIfNull(rows);
            if (rows.Count == 0)
                return ["(empty)"];

            var chunks = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < rows.Count; i++)
            {
                string row = rows[i];
                string addition = current.Length == 0 ? row : "<br>" + row;
                if (current.Length > 0 && current.Length + addition.Length > maxBodyLength)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                    addition = row;
                }

                // Single row longer than budget: still emit it alone (client may truncate).
                if (current.Length == 0 && addition.Length > maxBodyLength && chunks.Count > 0)
                {
                    chunks.Add(addition);
                    continue;
                }

                current.Append(addition);
            }

            if (current.Length > 0)
                chunks.Add(current.ToString());

            return chunks;
        }
    }

    /// <summary>
    /// Builds AOML <c>text://</c> popup links for a character's armed weapons (template stats only).
    /// One block per slot; a block is never split across popups.
    /// </summary>
    internal static class GetWeaponsAomlBuilder
    {
        const string Header = "#FFD700";
        const string Name = "#FFFFFF";
        const string Label = "#9CD6E4";
        const string Value = "#FFA040";
        const string Muted = "#8A8A8A";
        const string Tag = "#FF6060";

        public static IReadOnlyList<string> BuildChatLines(
            string subjectName,
            IReadOnlyDictionary<WeaponSlot, CharacterWeapon> weapons,
            int maxBodyLength = GetStatsAomlBuilder.DefaultMaxBodyLength)
        {
            ArgumentNullException.ThrowIfNull(weapons);
            if (maxBodyLength < 64)
                throw new ArgumentOutOfRangeException(nameof(maxBodyLength));

            string who = string.IsNullOrWhiteSpace(subjectName) ? "Target" : subjectName;
            string title = string.Format(CultureInfo.InvariantCulture, "{0} Weapons ({1})", who, weapons.Count);
            if (weapons.Count == 0)
                return [GetStatsAomlBuilder.BuildLink(Color(Muted, "(no weapons)"), title)];

            var blocks = new List<string>(weapons.Count);
            foreach (KeyValuePair<WeaponSlot, CharacterWeapon> entry in weapons)
                blocks.Add(FormatWeapon(entry.Key, entry.Value));

            // Blank line between weapons; ChunkRows joins rows with a single <br>.
            for (int i = 1; i < blocks.Count; i++)
                blocks[i] = "<br>" + blocks[i];

            IReadOnlyList<string> chunks = GetStatsAomlBuilder.ChunkRows(blocks, maxBodyLength);
            var lines = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                string body = chunks[i].StartsWith("<br>", StringComparison.Ordinal) ? chunks[i][4..] : chunks[i];
                string label = chunks.Count == 1
                    ? title
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1}/{2})", title, i + 1, chunks.Count);
                lines.Add(GetStatsAomlBuilder.BuildLink(body, label));
            }

            return lines;
        }

        static string FormatWeapon(WeaponSlot slot, CharacterWeapon weapon)
        {
            var text = new StringBuilder();
            text.Append(Color(Header, "[" + slot + "]"))
                .Append(' ').Append(Color(Muted, "wire " + weapon.WireSlot.ToString(CultureInfo.InvariantCulture)));
            if (weapon.IsSyntheticFist)
                text.Append(' ').Append(Color(Tag, "fist"));
            if (!string.IsNullOrEmpty(weapon.SawTagName))
                text.Append(' ').Append(Color(Tag, "saw " + weapon.SawTagName));

            AppendItem(text, null, weapon.Item);
            if (weapon.VisualHand != null)
                AppendItem(text, "Visual", weapon.VisualHand);
            if (weapon.RangeSource != null && !ReferenceEquals(weapon.RangeSource, weapon.Item))
                AppendItem(text, "Range from", weapon.RangeSource);
            return text.ToString();
        }

        static void AppendItem(StringBuilder text, string? role, Item? item)
        {
            text.Append("<br>");
            if (role != null)
                text.Append(Color(Label, role + ": "));
            if (item == null)
            {
                text.Append(Color(Muted, "(no item)"));
                return;
            }

            text.Append(Color(Name, item.Name))
                .Append(' ').Append(Color(Value, "QL " + item.Quality.ToString(CultureInfo.InvariantCulture)))
                .Append(' ').Append(Color(Muted, string.Format(
                    CultureInfo.InvariantCulture, "id {0}/{1} inst {2}", item.LowId, item.HighId, item.InstanceId)));

            int critBonus = item.GetStat(CharacterStat.DamageBonus);
            int amsCap = item.GetStat(CharacterStat.AMSCap);
            text.Append("<br>").Append(Field("Damage", string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}-{1}",
                    item.GetStat(CharacterStat.MinDamage),
                    item.GetStat(CharacterStat.MaxDamage))))
                .Append(Field("Crit", "+" + critBonus.ToString(CultureInfo.InvariantCulture)))
                .Append(Field("Type", DamageTypeName(item.GetStat(CharacterStat.DamageType))));
            if (amsCap > 0)
                text.Append(Field("AMS cap", amsCap.ToString(CultureInfo.InvariantCulture)));

            int initiative = item.GetStat(CharacterStat.InitiativeType);
            text.Append("<br>").Append(Field("Attack", Seconds(item.GetStat(CharacterStat.AttackDelay))))
                .Append(Field("Recharge", Seconds(item.GetStat(CharacterStat.RechargeDelay))))
                .Append(Field("Range", item.GetStat(CharacterStat.AttackRange).ToString(CultureInfo.InvariantCulture)))
                .Append(Field("Init", initiative > 0 ? ((CharacterStat)initiative).ToString() : "-"));
            text.Append("<br>").Append(Field("Flags", item.GetWeaponFlags().ToString()));
        }

        static string Field(string label, string value)
            => Color(Label, label + " ") + Color(Value, value) + "  ";

        static string Color(string color, string text)
            => "<font color=" + color + ">" + text + "</font>";

        /// <summary>AttackDelay / RechargeDelay are centiseconds.</summary>
        static string Seconds(int centiseconds)
            => (centiseconds / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "s";

        /// <summary>DamageType names the AC stat it is reduced by (90..97); 0 hits as melee.</summary>
        static string DamageTypeName(int damageType)
            => damageType switch
            {
                0 => "- (melee)",
                90 => "Projectile",
                91 => "Melee",
                92 => "Energy",
                93 => "Chemical",
                94 => "Radiation",
                95 => "Cold",
                96 => "Poison",
                97 => "Fire",
                _ => damageType.ToString(CultureInfo.InvariantCulture)
            };
    }

    /// <summary>Builds AOML <c>text://</c> popup links for a character's active NCU.</summary>
    internal static class GetBuffsAomlBuilder
    {
        public static IReadOnlyList<string> BuildChatLines(
            string subjectName,
            IReadOnlyList<Buff> buffs,
            int usedNcu,
            int maxNcu,
            DateTime nowUtc,
            int maxBodyLength = GetStatsAomlBuilder.DefaultMaxBodyLength)
        {
            ArgumentNullException.ThrowIfNull(buffs);
            if (maxBodyLength < 64)
                throw new ArgumentOutOfRangeException(nameof(maxBodyLength));

            string who = string.IsNullOrWhiteSpace(subjectName) ? "Target" : subjectName;
            string title = string.Format(
                CultureInfo.InvariantCulture,
                "{0} Buffs ({1}, NCU {2}/{3})",
                who,
                buffs.Count,
                usedNcu,
                maxNcu);

            if (buffs.Count == 0)
                return [GetStatsAomlBuilder.BuildLink("(no buffs)", title)];

            var rows = new List<string>(buffs.Count);
            for (int i = 0; i < buffs.Count; i++)
                rows.Add(FormatRow(buffs[i], nowUtc));

            IReadOnlyList<string> chunks = GetStatsAomlBuilder.ChunkRows(rows, maxBodyLength);
            var lines = new List<string>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                string label = chunks.Count == 1
                    ? title
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} ({1}/{2})",
                        title,
                        i + 1,
                        chunks.Count);
                lines.Add(GetStatsAomlBuilder.BuildLink(chunks[i], label));
            }

            return lines;
        }

        public static string FormatRow(Buff buff, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(buff);

            string name = string.IsNullOrWhiteSpace(buff.Name) ? "Nano" : buff.Name;
            string flags = string.Empty;
            if (buff.IsHostile)
                flags += " debuff";
            if (!buff.CanCancel)
                flags += " locked";

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1}) ncu={2} rem={3} strain={4} inst={5} src={6}{7}",
                name,
                buff.Id,
                buff.NcuCost,
                FormatRemaining(buff.RemainingCentiseconds(nowUtc)),
                buff.NanoStrain,
                buff.NanoInstance,
                buff.Source.Instance,
                flags);
        }

        public static string FormatRemaining(int remainingCentiseconds)
        {
            int totalSeconds = remainingCentiseconds / 100;
            if (totalSeconds < 0)
                totalSeconds = 0;

            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            if (hours > 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:D2}:{2:D2}", hours, minutes, seconds);

            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:D2}", minutes, seconds);
        }
    }
}
