namespace ZoneEngine_New.Core.NpcEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    using AORebirth.Core.Encryption;

    using static Accounts.MySqlAccountSanctionRepository;

    internal sealed record NpcEditorLogin(string? Username, string? Password);
    internal sealed record NpcEditorSessionReply(string Token, DateTimeOffset ExpiresAt, bool CanWrite, bool CanSave);

    internal sealed class NpcEditorSessions(
        Func<string, EditorCredentials?> loadAccount, TimeProvider time, int readLevel, int writeLevel,
        bool allowWrites, bool allowSave)
    {
        const int MaxSessions = 32;
        static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(20);
        readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
        readonly object _sync = new();

        internal sealed record Session(int AccountId, string Username, string CredentialVersion,
            DateTimeOffset ExpiresAt, bool CanWrite);

        internal NpcEditorSessionReply? Login(NpcEditorLogin login)
        {
            if (string.IsNullOrWhiteSpace(login.Username) || login.Username.Length > 64
                || string.IsNullOrEmpty(login.Password) || login.Password.Length > 256)
                return null;

            EditorCredentials? account = loadAccount(login.Username);
            if (account == null || account.GmLevel < readLevel
                || !string.Equals(account.Username, login.Username, StringComparison.OrdinalIgnoreCase)
                || !VerifyPassword(login.Password, account.PasswordHash))
                return null;

            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            DateTimeOffset now = time.GetUtcNow();
            var session = new Session(account.Id, account.Username, Digest(account.PasswordHash),
                now + SessionLifetime, allowWrites && account.GmLevel >= writeLevel);
            lock (_sync)
            {
                foreach (string expired in _sessions.Where(pair => pair.Value.ExpiresAt <= now
                    || pair.Value.AccountId == account.Id).Select(pair => pair.Key).ToArray())
                {
                    _sessions.Remove(expired);
                }
                if (_sessions.Count >= MaxSessions)
                    _sessions.Remove(_sessions.MinBy(pair => pair.Value.ExpiresAt).Key);
                _sessions.Add(Digest(token), session);
            }
            return new NpcEditorSessionReply(token, session.ExpiresAt, session.CanWrite, session.CanWrite && allowSave);
        }

        internal Session? Find(string token)
        {
            if (token.Length != 64)
                return null;

            lock (_sync)
            {
                string key = Digest(token);
                if (!_sessions.TryGetValue(key, out Session? session))
                    return null;
                if (session.ExpiresAt > time.GetUtcNow())
                    return session;
                _sessions.Remove(key);
                return null;
            }
        }

        internal bool CanMutate(string token, Session session, bool save)
        {
            if (!session.CanWrite || (save && !allowSave))
                return false;

            // Administrative mutations alone recheck privileges against the account store.
            EditorCredentials? account = loadAccount(session.Username);
            if (account == null || account.Id != session.AccountId || account.GmLevel < writeLevel
                || string.IsNullOrEmpty(account.PasswordHash) || Digest(account.PasswordHash) != session.CredentialVersion)
            {
                Revoke(token);
                return false;
            }
            return ReferenceEquals(Find(token), session);
        }

        internal void Revoke(string token)
        {
            lock (_sync)
            {
                _sessions.Remove(Digest(token));
            }
        }

        static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        static bool VerifyPassword(string supplied, string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return false;
            try
            {
                return PasswordHash.ValidatePassword(supplied, stored);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException
                or IndexOutOfRangeException or CryptographicException)
            {
                return false;
            }
        }
    }
}
