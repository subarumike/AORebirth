namespace ZoneEngine_New.Core.Characters
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Organizations;

    /// <summary>
    /// Retail /played and /born replies. Layout copied from the played-born capture (2026-11-10): the text envelope
    /// ends in ':' instead of the org 't', followed by a ref, the character name and the parameters.
    /// </summary>
    public static class CharacterPlayedBornText
    {
        const string Envelope = "~&!!!\":";
        const string PlayedRef = "&)+R9";
        const string BornRef = "#09!B";

        /// <summary>/played reply: days, hours, minutes and seconds as integer parameters.</summary>
        public static FormatFeedbackMessage Played(Identity recipient, string name, long playedSeconds)
        {
            long seconds = Math.Max(0, playedSeconds);
            string body = PlayedRef + OrgChatText.TextArg(name)
                + IntArg(seconds / 86400)
                + IntArg(seconds % 86400 / 3600)
                + IntArg(seconds % 3600 / 60)
                + IntArg(seconds % 60);
            return Feedback(recipient, Envelope + body + "~");
        }

        /// <summary>/born reply: the creation date formatted like the asctime text the retail client shows.</summary>
        public static FormatFeedbackMessage Born(Identity recipient, string name, long createdAtUnix)
        {
            DateTime created = DateTimeOffset.FromUnixTimeSeconds(createdAtUnix).UtcDateTime;
            string text = created.ToString("ddd MMM", CultureInfo.InvariantCulture)
                + " " + created.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2)
                + created.ToString(" HH:mm:ss yyyy", CultureInfo.InvariantCulture) + "\n";
            string body = BornRef + OrgChatText.TextArg(name) + OrgChatText.TextArg(text);
            return Feedback(recipient, Envelope + body + "~");
        }

        static string IntArg(long value)
            => "i" + OrgChatText.EncodeInt((uint)Math.Clamp(value, 0, uint.MaxValue));

        static FormatFeedbackMessage Feedback(Identity recipient, string formattedMessage) => new()
        {
            Identity = recipient,
            Unknown = 1,
            Unknown1 = 1,
            Unknown2 = 0,
            FormattedMessage = formattedMessage
        };
    }
}
