// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MailListEntry.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    /// <summary>
    /// One mailbox row / full message. Wire from Gamecode MailMessage 0x10125ec5 / 0x10125f5f
    /// and GameData ACGItem BinaryStream &lt;&lt; (4 ints: low, high, level, 0).
    /// </summary>
    public class MailListEntry
    {
        public MailListEntry()
        {
            this.From = string.Empty;
            this.Subject = string.Empty;
            this.Body = string.Empty;
            this.IsSummary = true;
        }

        /// <summary>BinaryStream &lt;&lt; unsigned __int64 mail id.</summary>
        public ulong MailId { get; set; }

        /// <summary>
        /// First int after id. Live Market receive capture keeps this 0; Sent/Expires use the
        /// wire ints historically named CreditsField/CodField (unix seconds).
        /// </summary>
        public int TimeField { get; set; }

        public string From { get; set; }

        public string Subject { get; set; }

        /// <summary>
        /// Legacy name: wire int after Subject. Live capture 20260715-Recive-mail-datetime-stamp:
        /// this is Sent unix time (UTC seconds), NOT gift credits. Credits are ExtendedField64.
        /// </summary>
        public int CreditsField { get; set; }

        /// <summary>
        /// Legacy name: wire int after Sent. Live capture: Expires unix time (UTC seconds),
        /// typically Sent + 2 days. Not COD amount — COD is negative ExtendedField64.
        /// </summary>
        public int CodField { get; set; }

        /// <summary>
        /// Live Market mail used 0x7C in this capture. bit0 = read icon.
        /// </summary>
        public int FlagsField { get; set; }

        /// <summary>True = inbox list row (wire byte 1). False = full detail (byte 0 + body/item).</summary>
        public bool IsSummary { get; set; }

        public int ExtendedField64 { get; set; }

        /// <summary>ACGItem template low (GameData ACGItem_t).</summary>
        public int AcgLow { get; set; }

        /// <summary>ACGItem template high.</summary>
        public int AcgHigh { get; set; }

        /// <summary>ACGItem level/QL.</summary>
        public int AcgLevel { get; set; }

        /// <summary>Item stack count for ItemSlotView (ExtendedField74 on the wire).</summary>
        public int ExtendedField74 { get; set; }

        public string Body { get; set; }
    }
}
