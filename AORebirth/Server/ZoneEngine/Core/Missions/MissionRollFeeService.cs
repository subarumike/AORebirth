namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Globalization;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    /// <summary>
    /// Mission-terminal roll fee. Capture 20260717-mission-terminal / Mission terminal2:
    /// each QuestAlternative request deducts credits equal to character level, then
    /// FormatFeedback "{N} credits were deducted from your account." (yellow system chat),
    /// then the 5-offer roll reply. Example: level 175 → 175 credits.
    /// </summary>
    internal static class MissionRollFeeService
    {
        /// <summary>
        /// Attempts to charge the roll fee. On success updates Cash and sends the yellow deduct message.
        /// On failure (not enough credits) sends a yellow notice and returns false — caller should not roll.
        /// </summary>
        public static bool TryChargeRollFee(ICharacter character, out int fee)
        {
            fee = 0;
            if (character == null)
            {
                return false;
            }

            int level = character.Stats[StatIds.level].Value;
            fee = level < 1 ? 1 : level;

            // Prefer live Value (what the client shows). BaseValue alone is often 0 after login
            // and made every roll fail with an empty terminal.
            long cashRaw = character.Stats[StatIds.cash].Value;
            if (character.Stats[StatIds.cash].BaseValue > cashRaw)
            {
                cashRaw = character.Stats[StatIds.cash].BaseValue;
            }

            int cashBefore = CashStatRules.Clamp(cashRaw);
            if (cashBefore < fee)
            {
                SendYellowFeedback(
                    character,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "You need {0} credits to request a mission.",
                        fee));
                MissionDiagnostics.Log(
                    "ROLL-FEE-FAIL char={0} fee={1} cash={2}",
                    character.Identity.Instance,
                    fee,
                    cashBefore);
                return false;
            }

            int cashAfter = CashStatRules.Clamp((long)cashBefore - fee);
            character.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);

            SendYellowFeedback(
                character,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} credits were deducted from your account.",
                    fee));

            MissionDiagnostics.Log(
                "ROLL-FEE char={0} fee={1} cashBefore={2} cashAfter={3}",
                character.Identity.Instance,
                fee,
                cashBefore,
                cashAfter);
            return true;
        }

        private static void SendYellowFeedback(ICharacter character, string plainText)
        {
            if (character == null || string.IsNullOrEmpty(plainText))
            {
                return;
            }

            // Capture: FormatFeedback alone paints yellow system chat (TokenBoard / insurance path).
            character.Send(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    FormattedMessage = TokenBoardRuntime.ToYellowSystemFeedback(plainText)
                });
        }
    }
}
