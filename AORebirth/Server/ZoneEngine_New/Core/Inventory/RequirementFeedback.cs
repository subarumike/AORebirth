namespace ZoneEngine_New.Core.Inventory
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;

    /// <summary>Tells a player which item requirements stopped an action.</summary>
    internal static class RequirementFeedback
    {
        /// <summary>
        /// Sends the failing checks of <paramref name="actionType"/> as feedback. Does nothing when the
        /// requirements pass (the action was refused for another reason). Falls back to the client's
        /// generic "check the requirements" line when no single check can be named. <paramref name="getStat"/>,
        /// <paramref name="resolve"/> and <paramref name="getTargetStat"/> are the readers the action was checked with
        /// (default: the player's stats); <paramref name="nameOf"/> names the perk or nano a state check refers to.
        /// </summary>
        public static void SendIfUnmet(Player player, ItemTemplate template, ActionType actionType,
            Func<CharacterStat, int>? getStat = null, Func<ItemRequirement, bool?>? resolve = null,
            Func<CharacterStat, int>? getTargetStat = null, Func<int, string?>? nameOf = null)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(template);

            getStat ??= stat => player.Stats.Get(stat);
            if (player.Session == null || template.MeetsActionRequirements(getStat, actionType, resolve, getTargetStat))
                return;

            IReadOnlyList<UnmetRequirement> unmet = template.UnmetActionRequirements(getStat, actionType, resolve, getTargetStat);
            if (unmet.Count == 0)
            {
                ClientFeedback.Send(player, ClientFeedback.CheckItemRequirements);
                return;
            }

            SendText(player, "Requirements not met: "
                + string.Join(", ", unmet.Select(entry => Describe(entry, nameOf))));
        }

        /// <summary>
        /// Plain server text. The client only renders FormatFeedback as "~&amp;" formatted text (raw text shows as a blank
        /// line), so it goes out as live sends plain text: 110/<see cref="ClientFeedback.PlainText"/> with the string.
        /// </summary>
        public static void SendText(Player player, string text)
        {
            ArgumentNullException.ThrowIfNull(player);
            ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, text);
        }

        static string Describe(UnmetRequirement entry, Func<int, string?>? nameOf)
        {
            ItemRequirement requirement = entry.Requirement;
            string Named(int id) => nameOf?.Invoke(id) is { Length: > 0 } name ? name : id.ToString(CultureInfo.InvariantCulture);
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
