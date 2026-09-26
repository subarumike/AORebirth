namespace AORebirth.Interfaces.Persistence.Mail
{
    using System;

    /// <summary>Detached mailbox row. Gameplay owns wire projection; DAO owns storage only.</summary>
    public sealed class MailRowData
    {
        public long MailId { get; set; }

        public int SenderCharacterId { get; set; }

        public string SenderName { get; set; }

        public int RecipientCharacterId { get; set; }

        public string RecipientName { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        public int AcgLow { get; set; }

        public int AcgHigh { get; set; }

        public int AcgLevel { get; set; }

        public int AcgMultipleCount { get; set; }

        public int Credits { get; set; }

        public byte ExpressFlag { get; set; }

        public bool IsRead { get; set; }

        public DateTime SentAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }
    }

    /// <summary>Insert payload before the provider assigns <see cref="MailRowData.MailId"/>.</summary>
    public sealed class MailInsertData
    {
        public int SenderCharacterId { get; set; }

        public string SenderName { get; set; }

        public int RecipientCharacterId { get; set; }

        public string RecipientName { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        public int AcgLow { get; set; }

        public int AcgHigh { get; set; }

        public int AcgLevel { get; set; }

        public int AcgMultipleCount { get; set; }

        public int Credits { get; set; }

        public byte ExpressFlag { get; set; }

        public DateTime SentAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }
    }
}
