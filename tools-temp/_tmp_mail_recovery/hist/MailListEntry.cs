// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MailListEntry.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    /// <summary>
    /// One mailbox row. Wire layout from Gamecode.dll MailMessage reader/writer
    /// (0x10125f5f / 0x10125ec5): uint64 id, int, From, Subject, 3 ints, summary byte.
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

        /// <summary>First int after id (likely sent/arrival time).</summary>
        public int TimeField { get; set; }

        public string From { get; set; }

        public string Subject { get; set; }

        /// <summary>Likely attached credits (positive).</summary>
        public int CreditsField { get; set; }

        /// <summary>Likely COD amount.</summary>
        public int CodField { get; set; }

        /// <summary>Likely delivery/flags (express etc.).</summary>
        public int FlagsField { get; set; }

        /// <summary>
        /// When true, wire byte=1 and body/item are omitted (inbox list rows).
        /// When false, body/item follow (Gamecode summary flag at MailMessage+1).
        /// </summary>
        public bool IsSummary { get; set; }

        public int ExtendedField64 { get; set; }

        public int ItemType { get; set; }

        public int ItemInstance { get; set; }

        public int ItemExtra { get; set; }

        public int ExtendedField74 { get; set; }

        public string Body { get; set; }
    }
}
