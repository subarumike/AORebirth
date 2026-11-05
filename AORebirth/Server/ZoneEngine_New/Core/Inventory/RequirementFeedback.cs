namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;

    /// <summary>Tells a player which item requirements stopped an action.</summary>
    internal static class RequirementFeedback
    {
        // The client's own requirement lines (category 110, FormatFeedback).
        /// <summary>"%s %s is required to be at least %d!": whose, the stat's client name, the lowest passing value.</summary>
        const int StatAtLeast = 147700404;

        /// <summary>"%s %s is not less than %d!": whose, the stat's client name, the limit.</summary>
        const int StatNotLessThan = 127970254;

        /// <summary>"%s must recently have %s running!": who, the nano's name.</summary>
        const int MustHaveNanoRunning = 6774439;

        /// <summary>"%s must not have %s running!": who, the nano's name.</summary>
        const int MustNotHaveNanoRunning = 116350327;

        /// <summary>"This effect can only be utilitized by %s.": the profession.</summary>
        const int OnlyUsableBy = 7658105;

        /// <summary>"This effect can't be utilitized by %s!": the profession.</summary>
        const int NotUsableBy = 171954057;

        /// <summary>"Target side must be %s!" / "Target side must not be %s!".</summary>
        const int TargetSideMustBe = 55195541;
        const int TargetSideMustNotBe = 59137061;

        /// <summary>"Target sex must be %s!".</summary>
        const int TargetSexMustBe = 150782277;

        /// <summary>"Target breed must be %s!".</summary>
        const int TargetBreedMustBe = 154478725;

        /// <summary>"You must be behind the target!".</summary>
        const int MustBeBehindTarget = 164102084;

        /// <summary>
        /// Sends the failing checks of <paramref name="actionType"/> as feedback. Does nothing when the
        /// requirements pass (the action was refused for another reason). Falls back to the client's
        /// generic "check the requirements" line when no single check can be named. <paramref name="getStat"/>,
        /// <paramref name="resolve"/> and <paramref name="getTargetStat"/> are the readers the action was checked with
        /// (default: the player's stats); <paramref name="nameOf"/> names the perk or nano a state check refers to
        /// (default: the item template's name).
        /// </summary>
        public static void SendIfUnmet(Player player, ItemTemplate template, ActionType actionType,
            Func<CharacterStat, int>? getStat = null, Func<ItemRequirement, bool?>? resolve = null,
            Func<CharacterStat, int>? getTargetStat = null, Func<int, string?>? nameOf = null,
            Func<ItemRequirement, bool?>? resolveTarget = null)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(template);

            getStat ??= stat => player.Stats.Get(stat);
            if (player.Session == null || template.MeetsActionRequirements(getStat, actionType, resolve, getTargetStat, resolveTarget))
                return;

            IReadOnlyList<UnmetRequirement> unmet = template.UnmetActionRequirements(getStat, actionType, resolve, getTargetStat, resolveTarget);
            if (unmet.Count == 0)
            {
                ClientFeedback.Send(player, ClientFeedback.CheckItemRequirements);
                return;
            }

            nameOf ??= TemplateName(player);
            // Each check once: an action can list the same one under several selectors.
            var sent = new HashSet<string>(StringComparer.Ordinal);
            var plain = new List<string>();
            foreach (UnmetRequirement entry in unmet)
            {
                if (TryClientLine(entry, nameOf, out int messageId, out object[] args))
                {
                    if (sent.Add(messageId + "|" + string.Join("|", args)))
                        ClientFeedback.SendFormatted(player, messageId, args);
                }
                else if (Describe(entry, nameOf) is { } line && sent.Add(line))
                {
                    plain.Add(line);
                }
            }

            if (plain.Count > 0)
                SendText(player, "Requirements not met: " + string.Join(", ", plain));
            else if (sent.Count == 0)
                ClientFeedback.Send(player, ClientFeedback.CheckItemRequirements);
        }

        /// <summary>
        /// Plain server text. The client only renders FormatFeedback as "~&amp;" formatted text (raw text shows as a blank
        /// line), so it goes out as live sends plain text: 110/<see cref="ClientFeedback.PlainText"/> with the string.
        /// </summary>
        public static void SendText(Player player, string text)
        {
            ArgumentNullException.ThrowIfNull(player);
            foreach (string line in WrapLines(text ?? string.Empty, ClientFeedback.MaxTextArgumentLength))
                ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, line);
        }

        /// <summary>
        /// <paramref name="text"/> in lines of at most <paramref name="max"/> characters, each one plain text message
        /// (see <see cref="ClientFeedback.MaxTextArgumentLength"/>). Breaks after ", " first, then at a space.
        /// </summary>
        internal static IEnumerable<string> WrapLines(string text, int max)
        {
            string rest = text;
            while (rest.Length > max)
            {
                int comma = rest.LastIndexOf(", ", max - 1, StringComparison.Ordinal);
                int space = rest.LastIndexOf(' ', max);
                int cut = comma > 0 ? comma + 1 : space > 0 ? space : max;
                yield return rest[..cut].TrimEnd();
                rest = rest[cut..].TrimStart();
            }

            yield return rest;
        }

        /// <summary>A nano or item named by its template, when the playfield's catalog has it.</summary>
        static Func<int, string?> TemplateName(Player player)
            => id => player.Playfield?.GetService<IItemTemplateCatalog>() is { } catalog
                && catalog.TryGet(id, out ItemTemplate named) && !string.IsNullOrEmpty(named.Name)
                    ? named.Name
                    : null;

        /// <summary>The client's own line for a check it has one for: stat minimums and limits, running nanos, profession,
        /// and the target's side, sex and breed.</summary>
        static bool TryClientLine(UnmetRequirement entry, Func<int, string?> nameOf, out int messageId, out object[] args)
        {
            ItemRequirement requirement = entry.Requirement;
            string who = entry.OnTarget ? "Your target" : "You";
            string whose = entry.OnTarget ? "Your target's" : "Your";
            var op = (Operator)requirement.Operator;
            messageId = 0;
            args = [];

            switch (op)
            {
                case Operator.HasRunningNano when nameOf(requirement.Value) is { } running:
                    (messageId, args) = (MustHaveNanoRunning, [who, running]);
                    return true;
                case Operator.HasNotRunningNano when nameOf(requirement.Value) is { } stopped:
                    (messageId, args) = (MustNotHaveNanoRunning, [who, stopped]);
                    return true;
            }

            var stat = (CharacterStat)requirement.StatNumber;
            switch (op)
            {
                // Stab and the like: [TargetFacing EqualTo 1], 1 while standing behind the target.
                case Operator.EqualTo when stat == CharacterStat.TargetFacing && requirement.Value == 1:
                    messageId = MustBeBehindTarget;
                    return true;
                case Operator.GreaterThan:
                    (messageId, args) = (StatAtLeast,
                        [whose, new ClientFeedback.TextReference(ClientFeedback.StatNameCategory, (int)stat), requirement.Value + 1]);
                    return true;
                case Operator.LessThan:
                    (messageId, args) = (StatNotLessThan,
                        [whose, new ClientFeedback.TextReference(ClientFeedback.StatNameCategory, (int)stat), requirement.Value]);
                    return true;
                case Operator.EqualTo or Operator.Unequal when !entry.OnTarget
                    && stat is CharacterStat.Profession or CharacterStat.VisualProfession:
                    (messageId, args) = (op == Operator.EqualTo ? OnlyUsableBy : NotUsableBy, [Spaced((Profession)requirement.Value)]);
                    return true;
                case Operator.EqualTo or Operator.Unequal when entry.OnTarget && stat == CharacterStat.Side:
                    (messageId, args) = (op == Operator.EqualTo ? TargetSideMustBe : TargetSideMustNotBe, [Spaced((Side)requirement.Value)]);
                    return true;
                case Operator.EqualTo when entry.OnTarget && stat == CharacterStat.Sex:
                    (messageId, args) = (TargetSexMustBe, [Spaced((Gender)requirement.Value)]);
                    return true;
                case Operator.EqualTo when entry.OnTarget && stat == CharacterStat.Breed:
                    (messageId, args) = (TargetBreedMustBe, [Spaced((Breed)requirement.Value)]);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>An enum value as words: MartialArtist becomes "Martial Artist".</summary>
        static string Spaced<T>(T value) where T : struct, Enum
            => Regex.Replace(value.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

        /// <summary>
        /// A check the client has no line for, in plain words. Null for a target test with no wording (it reads no stat,
        /// so "Flags N (needs Operator 0)" would mean nothing).
        /// </summary>
        static string? Describe(UnmetRequirement entry, Func<int, string?> nameOf)
        {
            ItemRequirement requirement = entry.Requirement;
            // Reads the action's target, whatever subject the leaf sits under.
            if ((Operator)requirement.Operator == Operator.MustBeCastersPet)
                return "target must be your pet";

            string Named(int id) => nameOf(id) is { Length: > 0 } name ? name : id.ToString(CultureInfo.InvariantCulture);
            string line = requirement.Value.ToString(CultureInfo.InvariantCulture);
            string? state = (Operator)requirement.Operator switch
            {
                Operator.HasPerk => "needs perk " + Named(requirement.Value),
                Operator.HasNotPerk => "must not have perk " + Named(requirement.Value),
                Operator.IsPerkLocked => Named(requirement.Value) + " must be on cooldown",
                Operator.IsPerkUnlocked => Named(requirement.Value) + " is on cooldown",
                Operator.HasRunningNano => "needs " + Named(requirement.Value) + " running",
                Operator.HasNotRunningNano => Named(requirement.Value) + " must not be running",
                Operator.HasRunningNanoLine => "needs a nano of line " + line + " running",
                Operator.HasNotRunningNanoLine => "no nano of line " + line + " may be running",
                Operator.IsPetOverEquipped => "needs an over-equipped pet",
                Operator.MustAlliedCombat => "an ally must be in combat",
                Operator.MustNotAlliedCombat => "no ally may be in combat",
                _ => null
            };
            if (state != null)
                return entry.OnTarget ? "target: " + state : state;

            // Other tests (IsSameAs, IsPet, ...) read no stat. Their leaf can sit under a Not ("not on yourself"), so a
            // failing leaf need not be what refused the action: say nothing rather than guess.
            if ((Operator)requirement.Operator is not (Operator.EqualTo or Operator.GreaterThan or Operator.LessThan
                or Operator.Unequal or Operator.BitAnd or Operator.NotBitAnd or Operator.TestNumPets))
                return null;

            var stat = (CharacterStat)requirement.StatNumber;
            int have = entry.Have;
            string value = requirement.Value.ToString(CultureInfo.InvariantCulture);
            string flag = "0x" + requirement.Value.ToString("X", CultureInfo.InvariantCulture);
            string need = (Operator)requirement.Operator switch
            {
                Operator.EqualTo => "= " + value,
                Operator.GreaterThan => "above " + value,
                Operator.LessThan => "below " + value,
                Operator.Unequal => "not " + value,
                Operator.BitAnd => "flag " + flag,
                Operator.NotBitAnd => "no flag " + flag,
                _ => ((Operator)requirement.Operator) + " " + value
            };

            return string.Format(CultureInfo.InvariantCulture, "{0}{1} {2} (needs {3})",
                entry.OnTarget ? "target " : string.Empty, stat, have, need);
        }
    }
}
