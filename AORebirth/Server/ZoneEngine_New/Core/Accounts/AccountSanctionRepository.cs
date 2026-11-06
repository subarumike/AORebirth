namespace ZoneEngine_New.Core.Accounts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Dapper;

    using MySqlConnector;

    using ZoneEngine_New.Core.Data;

    /// <summary>
    /// One account_sanctions row. <see cref="EndsAtUtc"/> null is a permanent ban, otherwise a suspension. It blocks
    /// login while <see cref="LiftedAtUtc"/> is null and it has not ended.
    /// </summary>
    public sealed record AccountSanction(
        long Id,
        string Account,
        DateTime IssuedAtUtc,
        DateTime? EndsAtUtc,
        string IssuedBy,
        string Reason,
        DateTime? LiftedAtUtc,
        string? LiftedBy,
        string? LiftReason)
    {
        public bool Permanent => EndsAtUtc == null;

        public bool IsActive(DateTime nowUtc) => LiftedAtUtc == null && (EndsAtUtc == null || EndsAtUtc > nowUtc);
    }

    /// <summary>
    /// An account that may not log in: <see cref="EndsAtUtc"/> is when its last active suspension ends, or null when it
    /// is banned (a permanent sanction, or a nonzero legacy login.Flags).
    /// </summary>
    public readonly record struct AccountBlock(string Account, DateTime? EndsAtUtc);

    /// <summary>What a lift found: whether the account exists, the sanctions lifted, and whether a legacy Flags ban was cleared.</summary>
    public readonly record struct SanctionLift(bool AccountFound, int Lifted, bool LegacyFlagsCleared);

    /// <summary>One character on an account, for <c>.account info</c>. Level and profession are 0 when not stored.</summary>
    public sealed record AccountCharacter(int Id, string Name, int Level, int Profession, int PlayfieldId, bool Online);

    /// <summary>A login account, for <c>.account info</c>.</summary>
    public sealed record AccountDetails(
        int Id,
        string Account,
        DateTime Created,
        string Email,
        int GmLevel,
        int Expansions,
        int AllowedCharacters,
        int Flags,
        IReadOnlyList<AccountCharacter> Characters);

    public interface IAccountSanctionRepository
    {
        /// <summary>The account and its characters, or null when no account has that name.</summary>
        AccountDetails? Details(string account);

        /// <summary>
        /// Records a sanction for <paramref name="account"/>: a suspension of <paramref name="seconds"/>, or a permanent ban
        /// when null. Null when no account has that name.
        /// </summary>
        AccountSanction? Issue(string account, long? seconds, string issuedBy, string reason);

        /// <summary>Lifts every active sanction of the account and clears a legacy Flags ban (the old account.ban).</summary>
        SanctionLift Lift(string account, string liftedBy, string reason);

        /// <summary>The account's sanctions, newest first, at most <paramref name="limit"/>; empty when it has none or does not exist.</summary>
        IReadOnlyList<AccountSanction> History(string account, int limit);

        /// <summary>Which of <paramref name="accounts"/> may not log in now.</summary>
        IReadOnlyList<AccountBlock> Blocked(IReadOnlyCollection<string> accounts);

        /// <summary>The account login name exactly as stored, or null when none matches (names compare ignoring case).</summary>
        string? FindAccount(string account);

        /// <summary>The account that owns the character named <paramref name="characterName"/>, or null.</summary>
        string? FindAccountOfCharacter(string characterName);
    }

    public sealed class MySqlAccountSanctionRepository : IAccountSanctionRepository
    {
        /// <summary>The columns stored as text are capped by the table; longer input is cut, never refused.</summary>
        public const int MaxIssuerLength = 64;

        public const int MaxReasonLength = 255;

        const string ActiveSanction =
            "s.LiftedAtUtc IS NULL AND (s.EndsAtUtc IS NULL OR s.EndsAtUtc > UTC_TIMESTAMP(6))";

        const string SelectSanction =
            "SELECT s.SanctionId AS Id, l.Username AS Account, s.IssuedAtUtc, s.EndsAtUtc, s.IssuedBy, s.Reason, "
            + "s.LiftedAtUtc, s.LiftedBy, s.LiftReason FROM account_sanctions s INNER JOIN login l ON l.Id=s.AccountId ";

        static MySqlConnection Open()
        {
            var connection = new MySqlConnection(MySqlConnectionSettings.GetRequiredConnectionString());
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public AccountSanction? Issue(string account, long? seconds, string issuedBy, string reason)
        {
            using MySqlConnection connection = Open();
            int rows = connection.Execute(
                "INSERT INTO account_sanctions (AccountId, IssuedAtUtc, EndsAtUtc, IssuedBy, Reason) "
                + "SELECT Id, UTC_TIMESTAMP(6), IF(@Seconds IS NULL, NULL, DATE_ADD(UTC_TIMESTAMP(6), INTERVAL @Seconds SECOND)), "
                + "@IssuedBy, @Reason FROM login WHERE Username=@Account LIMIT 1",
                new { Account = account, Seconds = seconds, IssuedBy = Cap(issuedBy, MaxIssuerLength), Reason = Cap(reason, MaxReasonLength) });
            if (rows == 0)
                return null;

            long id = connection.ExecuteScalar<long>("SELECT LAST_INSERT_ID()");
            return Map(connection.QuerySingle<Row>(SelectSanction + "WHERE s.SanctionId=@Id", new { Id = id }));
        }

        public SanctionLift Lift(string account, string liftedBy, string reason)
        {
            using MySqlConnection connection = Open();
            using MySqlTransaction transaction = connection.BeginTransaction();
            int? accountId = connection.QuerySingleOrDefault<int?>(
                "SELECT Id FROM login WHERE Username=@Account LIMIT 1 FOR UPDATE", new { Account = account }, transaction);
            if (accountId == null)
            {
                transaction.Commit();
                return new SanctionLift(false, 0, false);
            }

            int lifted = connection.Execute(
                "UPDATE account_sanctions s SET s.LiftedAtUtc=UTC_TIMESTAMP(6), s.LiftedBy=@LiftedBy, s.LiftReason=@Reason "
                + "WHERE s.AccountId=@AccountId AND " + ActiveSanction,
                new { AccountId = accountId.Value, LiftedBy = Cap(liftedBy, MaxIssuerLength), Reason = Cap(reason, MaxReasonLength) },
                transaction);
            // The pre-ledger account.ban wrote Flags 1; nothing else writes Flags after an account is created.
            int cleared = connection.Execute(
                "UPDATE login SET Flags=0 WHERE Id=@AccountId AND Flags=1", new { AccountId = accountId.Value }, transaction);
            transaction.Commit();
            return new SanctionLift(true, lifted, cleared > 0);
        }

        public IReadOnlyList<AccountSanction> History(string account, int limit)
        {
            using MySqlConnection connection = Open();
            return connection.Query<Row>(
                    SelectSanction + "WHERE l.Username=@Account ORDER BY s.SanctionId DESC LIMIT @Limit",
                    new { Account = account, Limit = Math.Clamp(limit, 1, 500) })
                .Select(Map)
                .ToArray();
        }

        public IReadOnlyList<AccountBlock> Blocked(IReadOnlyCollection<string> accounts)
        {
            if (accounts.Count == 0)
                return Array.Empty<AccountBlock>();

            using MySqlConnection connection = Open();
            return connection.Query<BlockRow>(
                    "SELECT l.Username AS Account, l.Flags AS Flags, CAST(SUM(s.SanctionId IS NOT NULL AND s.EndsAtUtc IS NULL) AS SIGNED) AS Permanent, "
                    + "MAX(s.EndsAtUtc) AS EndsAtUtc FROM login l "
                    + "LEFT JOIN account_sanctions s ON s.AccountId=l.Id AND " + ActiveSanction + " "
                    + "WHERE l.Username IN @Accounts GROUP BY l.Id, l.Username, l.Flags "
                    + "HAVING l.Flags<>0 OR COUNT(s.SanctionId) > 0",
                    new { Accounts = accounts.ToArray() })
                .Select(row => new AccountBlock(row.Account,
                    row.Flags != 0 || row.Permanent > 0 || row.EndsAtUtc == null ? null : Utc(row.EndsAtUtc.Value)))
                .ToArray();
        }

        public AccountDetails? Details(string account)
        {
            using MySqlConnection connection = Open();
            LoginRow? login = connection.QuerySingleOrDefault<LoginRow>(
                "SELECT Id, Username, CreationDate, Email, GM, Expansions, AllowedCharacters, Flags FROM login WHERE Username=@Account LIMIT 1",
                new { Account = account });
            if (login == null)
                return null;

            // A character's stats are keyed by its CanbeAffected identity.
            AccountCharacter[] characters = connection.Query<CharacterRow>(
                    "SELECT c.Id, c.Name, c.playfield AS PlayfieldId, CAST(COALESCE(c.Online, 0) AS SIGNED) AS Online, "
                    + "COALESCE(lv.StatValue, 0) AS Level, COALESCE(pr.StatValue, 0) AS Profession FROM characters c "
                    + "LEFT JOIN stats lv ON lv.Type=@Type AND lv.Instance=c.Id AND lv.StatId=@LevelStat "
                    + "LEFT JOIN stats pr ON pr.Type=@Type AND pr.Instance=c.Id AND pr.StatId=@ProfessionStat "
                    + "WHERE c.Username=@Account ORDER BY c.Name",
                    new
                    {
                        Account = login.Username,
                        Type = (int)SmokeLounge.AOtomation.Messaging.GameData.IdentityType.CanbeAffected,
                        LevelStat = (int)SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.Level,
                        ProfessionStat = (int)SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.Profession
                    })
                .Select(row => new AccountCharacter(row.Id, row.Name, row.Level, row.Profession, row.PlayfieldId, row.Online != 0))
                .ToArray();
            return new AccountDetails(login.Id, login.Username, login.CreationDate, login.Email, login.GM, login.Expansions,
                login.AllowedCharacters, login.Flags, characters);
        }

        public string? FindAccount(string account)
        {
            using MySqlConnection connection = Open();
            return connection.QuerySingleOrDefault<string?>(
                "SELECT Username FROM login WHERE Username=@Account LIMIT 1", new { Account = account });
        }

        public string? FindAccountOfCharacter(string characterName)
        {
            using MySqlConnection connection = Open();
            return connection.QuerySingleOrDefault<string?>(
                "SELECT Username FROM characters WHERE Name=@Name LIMIT 1", new { Name = characterName });
        }

        static string Cap(string? text, int length)
        {
            string value = (text ?? string.Empty).Trim();
            return value.Length <= length ? value : value[..length];
        }

        static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        static DateTime? Utc(DateTime? value) => value == null ? null : Utc(value.Value);

        static AccountSanction Map(Row row) => new(row.Id, row.Account, Utc(row.IssuedAtUtc), Utc(row.EndsAtUtc), row.IssuedBy,
            row.Reason, Utc(row.LiftedAtUtc), row.LiftedBy, row.LiftReason);

        sealed class Row
        {
            public long Id { get; set; }

            public string Account { get; set; } = string.Empty;

            public DateTime IssuedAtUtc { get; set; }

            public DateTime? EndsAtUtc { get; set; }

            public string IssuedBy { get; set; } = string.Empty;

            public string Reason { get; set; } = string.Empty;

            public DateTime? LiftedAtUtc { get; set; }

            public string? LiftedBy { get; set; }

            public string? LiftReason { get; set; }
        }

        sealed class LoginRow
        {
            public int Id { get; set; }

            public string Username { get; set; } = string.Empty;

            public DateTime CreationDate { get; set; }

            public string Email { get; set; } = string.Empty;

            public int GM { get; set; }

            public int Expansions { get; set; }

            public int AllowedCharacters { get; set; }

            public int Flags { get; set; }
        }

        sealed class CharacterRow
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public int PlayfieldId { get; set; }

            public long Online { get; set; }

            public int Level { get; set; }

            public int Profession { get; set; }
        }

        sealed class BlockRow
        {
            public string Account { get; set; } = string.Empty;

            public int Flags { get; set; }

            public long Permanent { get; set; }

            public DateTime? EndsAtUtc { get; set; }
        }
    }
}
