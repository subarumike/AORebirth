namespace ZoneEngine_New.Core.Missions;

using System;
using System.Globalization;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

internal static class GeneratedMissionTokenProjection
{
    internal static FormatFeedbackMessage Build(Identity owner, int percent)
    {
        if (owner.Type != IdentityType.CanbeAffected || owner.Instance <= 0 || percent < 0 || percent > 100)
            throw new ArgumentException("Exact owner and durable mission token percent required.");
        // Same accepted tracker wording and TokenBoardRuntime yellow-system encoding.
        string text = percent == 100 ? "Mission chance of token reward upped to 100% due to your heroic effort."
            : string.Format(CultureInfo.InvariantCulture, "Mission chance of token reward upped to {0}%.", percent);
        return new() { Identity = owner, Unknown = 1, Unknown1 = 0, Unknown2 = 0,
            FormattedMessage = "~&!!!\":!!!)<s" + (char)Math.Min(255, text.Length + 1) + text };
    }
}
