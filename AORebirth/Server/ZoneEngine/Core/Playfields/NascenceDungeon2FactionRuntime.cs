namespace AORebirth.Core.Playfields
{
    using System;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Controllers;

    /// <summary>
    /// Capture 20260823-182854: GOS (Guardian of Shadow) rises only after
    /// Infernal Vortexoid / Croaker of Solitude kills. FormatFeedback #gKrfR Li pair.
    /// </summary>
    internal static class NascenceDungeon2FactionRuntime
    {
        // Capture wire: faction Li template after qualifying kills.
        private const string CapturedFactionFeedback =
            "~&!!!\":#gKrfR!!!8U!!WYER!!!8P!!!\"Li!!!!c";

        internal static bool TryApplyKillFactionGain(ICharacter attacker, ICharacter victim)
        {
            if (attacker == null || victim == null)
            {
                return false;
            }

            if (victim.Controller is NPCController == false)
            {
                return false;
            }

            if (attacker.Playfield == null
                || !NascenceDungeon2Rules.IsDungeonPlayfield(attacker.Playfield.Identity.Instance))
            {
                return false;
            }

            if (!NascenceDungeon2Rules.GrantsGuardianOfShadowFaction(victim.Name))
            {
                return false;
            }

            try
            {
                int current = attacker.Stats[StatIds.gos].Value;
                attacker.Stats[StatIds.gos].Value =
                    current + NascenceDungeon2Rules.GuardianOfShadowFactionGainPerKill;
            }
            catch (Exception)
            {
                return false;
            }

            if (attacker.Controller != null && attacker.Controller.Client != null)
            {
                attacker.Controller.Client.SendCompressed(
                    new FormatFeedbackMessage
                    {
                        Identity = attacker.Identity,
                        Unknown1 = 0,
                        FormattedMessage = CapturedFactionFeedback,
                        Unknown2 = 0
                    },
                    attacker.Identity.Instance);
            }

            return true;
        }
    }
}
