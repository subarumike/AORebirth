namespace AORebirth.BotService
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    public sealed class BotCredentialManager
    {
        public const string CredentialFormat = "bot_v1_<32 lowercase hex public id>_<64 lowercase hex secret>";

        private const int Iterations = 120000;
        private const string Algorithm = "PBKDF2-SHA256";
        private readonly IBotIdentityRepository repository;
        private readonly Func<DateTime> utcNow;

        public BotCredentialManager(IBotIdentityRepository repository, Func<DateTime> utcNow = null)
        {
            this.repository = repository ?? throw new ArgumentNullException("repository");
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public BotCredentialIssue CreateInitial(BotPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException("principal");
            }

            if (principal.BotId == Guid.Empty || principal.CurrentCredentialVersion != 0)
            {
                throw new InvalidOperationException("A new bot principal and credential version zero are required.");
            }

            DateTime now = this.utcNow();
            principal.CurrentCredentialVersion = 1;
            principal.CreatedAtUtc = principal.CreatedAtUtc == default(DateTime) ? now : principal.CreatedAtUtc;
            principal.UpdatedAtUtc = now;
            this.repository.SavePrincipal(principal);
            return this.Issue(principal, now);
        }

        public BotCredentialIssue Rotate(Guid botId)
        {
            BotPrincipal principal = this.repository.FindPrincipal(botId);
            if (principal == null)
            {
                throw new InvalidOperationException("Bot principal was not found.");
            }

            DateTime now = this.utcNow();
            principal.CurrentCredentialVersion = checked(principal.CurrentCredentialVersion + 1);
            principal.UpdatedAtUtc = now;
            BotCredentialIssue issue = this.Issue(principal, now);
            this.repository.RevokeOtherCredentials(botId, issue.PublicCredentialId, now);
            this.repository.SavePrincipal(principal);
            return issue;
        }

        public void Revoke(string publicCredentialId)
        {
            this.repository.RevokeCredential(publicCredentialId, this.utcNow());
        }

        public void SetEnabled(Guid botId, bool enabled)
        {
            BotPrincipal principal = this.repository.FindPrincipal(botId);
            if (principal == null)
            {
                throw new InvalidOperationException("Bot principal was not found.");
            }

            principal.Enabled = enabled;
            principal.UpdatedAtUtc = this.utcNow();
            this.repository.SavePrincipal(principal);
        }

        private BotCredentialIssue Issue(BotPrincipal principal, DateTime now)
        {
            byte[] publicBytes = RandomBytes(16);
            byte[] secretBytes = RandomBytes(32);
            byte[] salt = RandomBytes(16);
            string publicId = ToHex(publicBytes);
            string secret = ToHex(secretBytes);
            string credential = "bot_v1_" + publicId + "_" + secret;
            byte[] verifier = DeriveVerifier(principal.BotId, principal.CurrentCredentialVersion, secret, salt, Iterations);

            this.repository.SaveCredential(
                new BotCredentialRecord
                {
                    PublicCredentialId = publicId,
                    BotId = principal.BotId,
                    Version = principal.CurrentCredentialVersion,
                    Algorithm = Algorithm,
                    Iterations = Iterations,
                    Salt = salt,
                    Verifier = verifier,
                    CreatedAtUtc = now,
                    Revoked = false
                });

            return new BotCredentialIssue
            {
                Credential = credential,
                PublicCredentialId = publicId,
                BotId = principal.BotId,
                Version = principal.CurrentCredentialVersion
            };
        }

        internal static bool TryParse(string credential, out string publicId, out string secret)
        {
            publicId = null;
            secret = null;
            if (string.IsNullOrEmpty(credential))
            {
                return false;
            }

            string[] parts = credential.Split('_');
            if (parts.Length != 4
                || !string.Equals(parts[0], "bot", StringComparison.Ordinal)
                || !string.Equals(parts[1], "v1", StringComparison.Ordinal)
                || !IsLowerHex(parts[2], 32)
                || !IsLowerHex(parts[3], 64))
            {
                return false;
            }

            publicId = parts[2];
            secret = parts[3];
            return true;
        }

        internal static byte[] DeriveVerifier(Guid botId, int version, string secret, byte[] salt, int iterations)
        {
            string boundSecret = botId.ToString("N")
                + ":"
                + version.ToString(CultureInfo.InvariantCulture)
                + ":"
                + secret;
            using (Rfc2898DeriveBytes derivation = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(boundSecret),
                salt,
                iterations,
                HashAlgorithmName.SHA256))
            {
                return derivation.GetBytes(32);
            }
        }

        internal static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            int difference = left.Length ^ right.Length;
            int length = Math.Min(left.Length, right.Length);
            for (int index = 0; index < length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }

        private static byte[] RandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return bytes;
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static bool IsLowerHex(string value, int expectedLength)
        {
            if (value == null || value.Length != expectedLength)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class BotCredentialAuthenticator
    {
        private readonly IBotIdentityRepository repository;
        private readonly IBotAuditSink audit;
        private readonly Func<DateTime> utcNow;

        public BotCredentialAuthenticator(
            IBotIdentityRepository repository,
            IBotAuditSink audit,
            Func<DateTime> utcNow = null)
        {
            this.repository = repository ?? throw new ArgumentNullException("repository");
            this.audit = audit ?? throw new ArgumentNullException("audit");
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public BotAuthenticationResult Authenticate(string suppliedCredential)
        {
            string publicId;
            string secret;
            if (!BotCredentialManager.TryParse(suppliedCredential, out publicId, out secret))
            {
                return this.Fail(null, null, BotAuthenticationFailure.InvalidCredential);
            }

            BotCredentialRecord credential = this.repository.FindCredential(publicId);
            if (credential == null)
            {
                return this.Fail(null, null, BotAuthenticationFailure.InvalidCredential);
            }

            BotPrincipal principal = this.repository.FindPrincipal(credential.BotId);
            if (principal == null)
            {
                return this.Fail(credential.BotId, null, BotAuthenticationFailure.InvalidCredential);
            }

            if (!principal.Enabled)
            {
                return this.Fail(principal.BotId, principal, BotAuthenticationFailure.DisabledBot);
            }

            if (credential.Revoked)
            {
                return this.Fail(principal.BotId, principal, BotAuthenticationFailure.RevokedCredential);
            }

            if (credential.Version != principal.CurrentCredentialVersion)
            {
                return this.Fail(principal.BotId, principal, BotAuthenticationFailure.StaleCredentialVersion);
            }

            byte[] candidate = BotCredentialManager.DeriveVerifier(
                credential.BotId,
                credential.Version,
                secret,
                credential.Salt,
                credential.Iterations);
            if (!BotCredentialManager.FixedTimeEquals(candidate, credential.Verifier))
            {
                return this.Fail(principal.BotId, principal, BotAuthenticationFailure.InvalidCredential);
            }

            this.audit.Record(CreateAudit(
                BotAuditKind.AuthenticationSuccess,
                principal,
                null,
                true,
                "AUTHENTICATED",
                this.utcNow()));
            return new BotAuthenticationResult
            {
                Succeeded = true,
                Failure = BotAuthenticationFailure.None,
                Principal = principal,
                Credential = credential
            };
        }

        private BotAuthenticationResult Fail(
            Guid? botId,
            BotPrincipal principal,
            BotAuthenticationFailure failure)
        {
            BotAuditEvent auditEvent = CreateAudit(
                BotAuditKind.AuthenticationFailure,
                principal,
                null,
                false,
                failure.ToString().ToUpperInvariant(),
                this.utcNow());
            auditEvent.BotId = botId;
            this.audit.Record(auditEvent);
            return new BotAuthenticationResult { Succeeded = false, Failure = failure };
        }

        internal static BotAuditEvent CreateAudit(
            BotAuditKind kind,
            BotPrincipal principal,
            BotSession session,
            bool succeeded,
            string reasonCode,
            DateTime timestampUtc)
        {
            return new BotAuditEvent
            {
                Kind = kind,
                BotId = principal == null ? (Guid?)null : principal.BotId,
                AccountId = principal == null ? (long?)null : principal.OwningAccountId,
                OrganizationId = principal == null ? (long?)null : principal.OrganizationId,
                SessionId = session == null ? (Guid?)null : session.SessionId,
                Succeeded = succeeded,
                ReasonCode = reasonCode,
                TimestampUtc = timestampUtc,
                AuditIdentity = principal == null ? null : principal.AuditIdentity
            };
        }
    }

    public sealed class InMemoryBotIdentityRepository : IBotIdentityRepository
    {
        private readonly object sync = new object();
        private readonly Dictionary<Guid, BotPrincipal> principals = new Dictionary<Guid, BotPrincipal>();
        private readonly Dictionary<string, BotCredentialRecord> credentials =
            new Dictionary<string, BotCredentialRecord>(StringComparer.Ordinal);

        public BotPrincipal FindPrincipal(Guid botId)
        {
            lock (this.sync)
            {
                BotPrincipal principal;
                return this.principals.TryGetValue(botId, out principal) ? principal.Copy() : null;
            }
        }

        public BotCredentialRecord FindCredential(string publicCredentialId)
        {
            lock (this.sync)
            {
                BotCredentialRecord credential;
                return publicCredentialId != null
                    && this.credentials.TryGetValue(publicCredentialId, out credential)
                    ? credential.Copy()
                    : null;
            }
        }

        public void SavePrincipal(BotPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException("principal");
            }

            lock (this.sync)
            {
                this.principals[principal.BotId] = principal.Copy();
            }
        }

        public void SaveCredential(BotCredentialRecord credential)
        {
            if (credential == null || string.IsNullOrEmpty(credential.PublicCredentialId))
            {
                throw new ArgumentException("A public credential id is required.", "credential");
            }

            lock (this.sync)
            {
                this.credentials[credential.PublicCredentialId] = credential.Copy();
            }
        }

        public void RevokeCredential(string publicCredentialId, DateTime revokedAtUtc)
        {
            lock (this.sync)
            {
                BotCredentialRecord credential;
                if (publicCredentialId != null && this.credentials.TryGetValue(publicCredentialId, out credential))
                {
                    credential.Revoked = true;
                    credential.RevokedAtUtc = revokedAtUtc;
                }
            }
        }

        public void RevokeOtherCredentials(Guid botId, string exceptPublicCredentialId, DateTime revokedAtUtc)
        {
            lock (this.sync)
            {
                foreach (BotCredentialRecord credential in this.credentials.Values)
                {
                    if (credential.BotId == botId
                        && !string.Equals(
                            credential.PublicCredentialId,
                            exceptPublicCredentialId,
                            StringComparison.Ordinal))
                    {
                        credential.Revoked = true;
                        credential.RevokedAtUtc = revokedAtUtc;
                    }
                }
            }
        }
    }
}
