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
        /// Sent date as whole days since 1970-01-01 (GUI boost date; shows YYYY-Mon-DD 00:00).
        /// Inbox Expires column is this value + 2 days.
        /// </summary>
        public int TimeField { get; set; }

        public string From { get; set; }

        public string Subject { get; set; }

        public int CreditsField { get; set; }

        public int CodField { get; set; }

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
