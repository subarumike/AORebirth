namespace AORebirth.Interfaces.Persistence.Mail
{
    using System.Collections.Generic;

    /// <summary>
    /// Durable Mail Terminal inbox. Implementations own all SQL; callers never embed queries.
    /// Persistence failures propagate and must not be treated as empty mailboxes.
    /// </summary>
    public interface IMailDao
    {
        /// <summary>
        /// Ensures <c>character_mail</c> exists by applying <c>SqlTables/character_mail.sql</c>
        /// (<c>CREATE TABLE IF NOT EXISTS</c>). Safe to call on every ZoneEngine_New start.
        /// </summary>
        void EnsureSchema();

        /// <summary>Inserts one row and returns the assigned mail id.</summary>
        long Insert(MailInsertData mail);

        /// <summary>
        /// Purges expired rows for the recipient, then returns up to <paramref name="limit"/>
        /// remaining inbox rows ordered by SentAtUtc ascending.
        /// </summary>
        IList<MailRowData> ListInbox(int recipientCharacterId, int limit);

        /// <summary>Returns one owned row or null when absent / wrong recipient.</summary>
        MailRowData GetOwned(int recipientCharacterId, long mailId);

        /// <summary>Unread count after expiry purge for the recipient.</summary>
        int CountUnread(int recipientCharacterId);

        /// <summary>Marks the owned row read. Returns false when missing.</summary>
        bool MarkRead(int recipientCharacterId, long mailId);

        /// <summary>
        /// Clears attachments/credits on an owned row after Take All. Returns false when missing.
        /// </summary>
        bool ClearAttachments(int recipientCharacterId, long mailId, bool markRead);

        /// <summary>
        /// Deletes an owned row only when it has no remaining item/credit attachments.
        /// Returns false when missing or still attached.
        /// </summary>
        bool TryDeleteEmpty(int recipientCharacterId, long mailId);

        /// <summary>Deletes an owned row regardless of attachments (return-to-sender source).</summary>
        bool Delete(int recipientCharacterId, long mailId);
    }
}
