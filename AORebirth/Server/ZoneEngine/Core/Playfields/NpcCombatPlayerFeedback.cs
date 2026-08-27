namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// Capture-backed red general chat: "{victim} attacked by {attacker}!" (FormatFeedback gM*@s wire).
    /// </summary>
    internal static class NpcCombatPlayerFeedback
    {
        internal static void AnnounceAttackedBy(ICharacter victim, ICharacter attacker)
        {
            if (victim == null
                || attacker == null
                || victim.Controller == null
                || victim.Controller.Client == null)
            {
                return;
            }

            string victimName = string.IsNullOrEmpty(victim.Name) ? "you" : victim.Name;
            string attackerName = string.IsNullOrEmpty(attacker.Name) ? "enemy" : attacker.Name;
            if (victimName.Length > 254)
            {
                victimName = victimName.Substring(0, 254);
            }

            if (attackerName.Length > 254)
            {
                attackerName = attackerName.Substring(0, 254);
            }

            // Capture 20260731-005116 / pet-chat: ~&!!!":$gM*@s… → red "{victim} attacked by {attacker}!"
            // Send only to the victim client (not Playfield.Announce) so General shows red feedback
            // instead of a yellow playfield-wide system line.
            string formatted = string.Format(
                CultureInfo.InvariantCulture,
                "~&!!!\":$gM*@s{0}{1}s{2}{3}~",
                (char)(victimName.Length + 1),
                victimName,
                (char)(attackerName.Length + 1),
                attackerName);

            victim.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = victim.Identity,
                    Unknown1 = 0,
                    FormattedMessage = formatted,
                    Unknown2 = 0
                },
                victim.Identity.Instance);
        }
    }
}
