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
        /// generic "check the requirements" line when no single check can be named.
        /// </summary>
        public static void SendIfUnmet(Player player, ItemTemplate template, ActionType actionType)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(template);

            if (player.Session == null || template.MeetsActionRequirements(stat => player.Stats.Get(stat), actionType))
                return;

            IReadOnlyList<ItemRequirement> unmet = template.UnmetActionRequirements(stat => player.Stats.Get(stat), actionType);
            if (unmet.Count == 0)
            {
                ClientFeedback.Send(player, ClientFeedback.CheckItemRequirements);
                return;
            }

            SendText(player, "Requirements not met: "
                + string.Join(", ", unmet.Select(requirement => Describe(player, requirement))));
        }

        public static void SendText(Player player, string text)
        {
            ArgumentNullException.ThrowIfNull(player);
            player.Session?.Send(new FormatFeedbackMessage
            {
                Identity = player.Identity,
                Unknown = 1,
                Unknown1 = 0,
                FormattedMessage = text
            });
        }

        static string Describe(Player player, ItemRequirement requirement)
        {
            var stat = (CharacterStat)requirement.StatNumber;
            int have = player.Stats.GetOrZero(stat);
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

            return string.Format(CultureInfo.InvariantCulture, "{0} {1} (needs {2})", stat, have, need);
        }
    }
}
