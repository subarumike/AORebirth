namespace AORebirth.Database.Domain.Mail
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using AORebirth.Interfaces.Persistence.Mail;
    using Dapper;

    /// <summary>
    /// MySQL-backed Mail Terminal inbox. All SQL stays inside this DAO.
    /// Uses Dapper 1.13-compatible APIs (Query/Execute only) for AORebirth.Database.
    /// </summary>
    public sealed class MySqlMailDao : IMailDao
    {
        const string CharacterMailSqlFileName = "character_mail.sql";

        const string InsertSql = @"
INSERT INTO character_mail
  (SenderCharacterId, SenderName, RecipientCharacterId, RecipientName, Subject, Body,
   AcgLow, AcgHigh, AcgLevel, AcgMultipleCount, Credits, ExpressFlag, IsRead, SentAtUtc, ExpiresAtUtc)
VALUES
  (@SenderCharacterId, @SenderName, @RecipientCharacterId, @RecipientName, @Subject, @Body,
   @AcgLow, @AcgHigh, @AcgLevel, @AcgMultipleCount, @Credits, @ExpressFlag, 0, @SentAtUtc, @ExpiresAtUtc)";

        const string LastInsertIdSql = "SELECT LAST_INSERT_ID()";

        const string ListSql = @"
SELECT MailId, SenderCharacterId, SenderName, RecipientCharacterId, RecipientName,
       Subject, Body, AcgLow, AcgHigh, AcgLevel, AcgMultipleCount, Credits, ExpressFlag,
       IsRead, SentAtUtc, ExpiresAtUtc
FROM character_mail
WHERE RecipientCharacterId = @RecipientCharacterId
  AND ExpiresAtUtc >= @NowUtc
ORDER BY SentAtUtc ASC
LIMIT @Limit";

        const string GetSql = @"
SELECT MailId, SenderCharacterId, SenderName, RecipientCharacterId, RecipientName,
       Subject, Body, AcgLow, AcgHigh, AcgLevel, AcgMultipleCount, Credits, ExpressFlag,
       IsRead, SentAtUtc, ExpiresAtUtc
FROM character_mail
WHERE MailId = @MailId AND RecipientCharacterId = @RecipientCharacterId
LIMIT 1";

        const string PurgeSql = @"
DELETE FROM character_mail
WHERE RecipientCharacterId = @RecipientCharacterId
  AND ExpiresAtUtc < @NowUtc";

        const string CountUnreadSql = @"
SELECT COUNT(*)
FROM character_mail
WHERE RecipientCharacterId = @RecipientCharacterId
  AND ExpiresAtUtc >= @NowUtc
  AND IsRead = 0";

        const string MarkReadSql = @"
UPDATE character_mail
SET IsRead = 1
WHERE MailId = @MailId AND RecipientCharacterId = @RecipientCharacterId";

        const string ClearAttachmentsSql = @"
UPDATE character_mail
SET AcgLow = 0, AcgHigh = 0, AcgLevel = 0, AcgMultipleCount = 0, Credits = 0, IsRead = @IsRead
WHERE MailId = @MailId AND RecipientCharacterId = @RecipientCharacterId";

        const string DeleteEmptySql = @"
DELETE FROM character_mail
WHERE MailId = @MailId
  AND RecipientCharacterId = @RecipientCharacterId
  AND AcgLow = 0 AND AcgHigh = 0 AND Credits = 0";

        const string DeleteSql = @"
DELETE FROM character_mail
WHERE MailId = @MailId AND RecipientCharacterId = @RecipientCharacterId";

        readonly Func<IDbConnection> _connectionFactory;

        public MySqlMailDao(Func<IDbConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public void EnsureSchema()
        {
            string sqlPath = ResolveCharacterMailSqlPath();
            string sql = File.ReadAllText(sqlPath);
            if (string.IsNullOrWhiteSpace(sql))
                throw new InvalidOperationException("character_mail.sql is empty: " + sqlPath);

            WithConnection(connection =>
            {
                connection.Execute(sql);
                return 0;
            });
        }

        public long Insert(MailInsertData mail)
        {
            if (mail == null)
                throw new ArgumentNullException(nameof(mail));

            return WithConnection(connection =>
            {
                connection.Execute(InsertSql, new
                {
                    mail.SenderCharacterId,
                    SenderName = mail.SenderName ?? string.Empty,
                    mail.RecipientCharacterId,
                    RecipientName = mail.RecipientName ?? string.Empty,
                    Subject = mail.Subject ?? string.Empty,
                    Body = mail.Body ?? string.Empty,
                    mail.AcgLow,
                    mail.AcgHigh,
                    mail.AcgLevel,
                    mail.AcgMultipleCount,
                    mail.Credits,
                    mail.ExpressFlag,
                    mail.SentAtUtc,
                    mail.ExpiresAtUtc
                });
                long id = connection.Query<long>(LastInsertIdSql).Single();
                if (id <= 0)
                    throw new InvalidOperationException("Mail insert did not return a mail id.");
                return id;
            });
        }

        public IList<MailRowData> ListInbox(int recipientCharacterId, int limit)
        {
            if (recipientCharacterId <= 0)
                throw new ArgumentOutOfRangeException(nameof(recipientCharacterId));
            if (limit <= 0)
                return new List<MailRowData>();

            DateTime now = DateTime.UtcNow;
            return WithConnection(connection =>
            {
                connection.Execute(PurgeSql, new { RecipientCharacterId = recipientCharacterId, NowUtc = now });
                return connection.Query<MailRowData>(ListSql, new
                {
                    RecipientCharacterId = recipientCharacterId,
                    NowUtc = now,
                    Limit = limit
                }).ToList();
            });
        }

        public MailRowData GetOwned(int recipientCharacterId, long mailId)
        {
            if (recipientCharacterId <= 0 || mailId <= 0)
                return null;

            DateTime now = DateTime.UtcNow;
            return WithConnection(connection =>
            {
                connection.Execute(PurgeSql, new { RecipientCharacterId = recipientCharacterId, NowUtc = now });
                return connection.Query<MailRowData>(GetSql, new
                {
                    MailId = mailId,
                    RecipientCharacterId = recipientCharacterId
                }).FirstOrDefault();
            });
        }

        public int CountUnread(int recipientCharacterId)
        {
            if (recipientCharacterId <= 0)
                return 0;

            DateTime now = DateTime.UtcNow;
            return WithConnection(connection =>
            {
                connection.Execute(PurgeSql, new { RecipientCharacterId = recipientCharacterId, NowUtc = now });
                return connection.Query<int>(CountUnreadSql, new
                {
                    RecipientCharacterId = recipientCharacterId,
                    NowUtc = now
                }).Single();
            });
        }

        public bool MarkRead(int recipientCharacterId, long mailId)
        {
            if (recipientCharacterId <= 0 || mailId <= 0)
                return false;

            return WithConnection(connection =>
                connection.Execute(MarkReadSql, new
                {
                    MailId = mailId,
                    RecipientCharacterId = recipientCharacterId
                }) > 0);
        }

        public bool ClearAttachments(int recipientCharacterId, long mailId, bool markRead)
        {
            if (recipientCharacterId <= 0 || mailId <= 0)
                return false;

            return WithConnection(connection =>
                connection.Execute(ClearAttachmentsSql, new
                {
                    MailId = mailId,
                    RecipientCharacterId = recipientCharacterId,
                    IsRead = markRead ? 1 : 0
                }) > 0);
        }

        public bool TryDeleteEmpty(int recipientCharacterId, long mailId)
        {
            if (recipientCharacterId <= 0 || mailId <= 0)
                return false;

            return WithConnection(connection =>
                connection.Execute(DeleteEmptySql, new
                {
                    MailId = mailId,
                    RecipientCharacterId = recipientCharacterId
                }) > 0);
        }

        public bool Delete(int recipientCharacterId, long mailId)
        {
            if (recipientCharacterId <= 0 || mailId <= 0)
                return false;

            return WithConnection(connection =>
                connection.Execute(DeleteSql, new
                {
                    MailId = mailId,
                    RecipientCharacterId = recipientCharacterId
                }) > 0);
        }

        T WithConnection<T>(Func<IDbConnection, T> action)
        {
            using (IDbConnection connection = _connectionFactory())
            {
                if (connection.State != ConnectionState.Open)
                    connection.Open();
                return action(connection);
            }
        }

        static string ResolveCharacterMailSqlPath()
        {
            var candidates = new List<string>();
            string baseDir = AppContext.BaseDirectory ?? string.Empty;
            if (baseDir.Length > 0)
            {
                candidates.Add(Path.Combine(baseDir, "SqlTables", CharacterMailSqlFileName));
                candidates.Add(Path.Combine(baseDir, "ZoneEngine_New", "SqlTables", CharacterMailSqlFileName));
            }

            string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(assemblyDir))
                candidates.Add(Path.Combine(assemblyDir, "SqlTables", CharacterMailSqlFileName));

            // Dev tree: .../AORebirth.Database/Domain/Mail -> SqlTables/
            string? sourceDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(sourceDir))
            {
                candidates.Add(Path.GetFullPath(Path.Combine(sourceDir, "..", "..", "..", "SqlTables", CharacterMailSqlFileName)));
            }

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException(
                "character_mail.sql not found. Expected under SqlTables next to ZoneEngine_New output.",
                CharacterMailSqlFileName);
        }
    }
}
