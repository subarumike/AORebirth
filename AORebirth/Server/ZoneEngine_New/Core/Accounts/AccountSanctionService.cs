namespace ZoneEngine_New.Core.Accounts
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>What issuing a sanction did: the row, or why none was written, and how many characters were removed here.</summary>
    public sealed record SanctionIssueResult(AccountSanction? Sanction, string Error, int Removed)
    {
        public bool Ok => Sanction != null;
    }

    /// <summary>
    /// Account suspensions and bans (account_sanctions). LoginEngine, the account broker and this zone's admission refuse a
    /// sanctioned account; issuing one also removes its characters in play on this instance at once, and a sweep removes
    /// them from every instance within <see cref="SweepInterval"/> (a sanction issued elsewhere, or written straight to the
    /// database). A suspension ends by itself when its time passes. All database work runs on worker threads, never a tick.
    /// </summary>
    public sealed class AccountSanctionService : IDisposable
    {
        /// <summary>The longest suspension: ten years. Longer is a ban.</summary>
        public const long MaxSuspensionSeconds = 10L * 365 * 24 * 60 * 60;

        public const long MinSuspensionSeconds = 60;

        public static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(60);

        readonly IAccountSanctionRepository _repository;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly IZoneLogger _logger;
        readonly Timer _sweep;
        int _sweeping;

        public AccountSanctionService(IAccountSanctionRepository repository, Lazy<PlayfieldManager> playfields, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(playfields);
            ArgumentNullException.ThrowIfNull(logger);
            _repository = repository;
            _playfields = playfields;
            _logger = logger;
            _sweep = new Timer(_ => Sweep(), null, SweepInterval, SweepInterval);
        }

        /// <summary>
        /// Records a suspension of <paramref name="seconds"/>, or a permanent ban when null, and removes the account's
        /// characters in play here. <paramref name="issuedBy"/> and <paramref name="reason"/> are kept on the row.
        /// </summary>
        public Task<SanctionIssueResult> Issue(string account, long? seconds, string issuedBy, string reason)
        {
            string name = (account ?? string.Empty).Trim();
            if (name.Length == 0)
                return Task.FromResult(new SanctionIssueResult(null, "An account name is needed.", 0));
            if (seconds is < MinSuspensionSeconds or > MaxSuspensionSeconds)
                return Task.FromResult(new SanctionIssueResult(null, string.Format(CultureInfo.InvariantCulture,
                    "A suspension lasts {0} seconds to {1} days.", MinSuspensionSeconds, MaxSuspensionSeconds / 86400), 0));

            return Task.Run(() =>
            {
                AccountSanction? sanction = _repository.Issue(name, seconds, issuedBy, reason);
                if (sanction == null)
                    return new SanctionIssueResult(null, "No account named '" + name + "'.", 0);

                _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                    "Account sanction #{0} {1} account={2} until={3} by={4} reason={5}", sanction.Id,
                    sanction.Permanent ? "ban" : "suspension", sanction.Account, Describe(sanction.EndsAtUtc), sanction.IssuedBy,
                    sanction.Reason));
                int removed = Remove(sanction.Account, sanction.EndsAtUtc, sanction.Reason);
                return new SanctionIssueResult(sanction, string.Empty, removed);
            });
        }

        /// <summary>Lifts every active sanction of the account (and a legacy Flags ban). The rows stay as history.</summary>
        public Task<SanctionLift> Lift(string account, string liftedBy, string reason)
        {
            string name = (account ?? string.Empty).Trim();
            if (name.Length == 0)
                return Task.FromResult(new SanctionLift(false, 0, false));

            return Task.Run(() =>
            {
                SanctionLift lift = _repository.Lift(name, liftedBy, reason);
                if (lift.AccountFound)
                    _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Account sanctions lifted account={0} lifted={1} legacyFlags={2} by={3} reason={4}",
                        name, lift.Lifted, lift.LegacyFlagsCleared, liftedBy, reason));
                return lift;
            });
        }

        /// <summary>The account's sanctions, newest first.</summary>
        public Task<IReadOnlyList<AccountSanction>> History(string account, int limit = 20)
        {
            string name = (account ?? string.Empty).Trim();
            return name.Length == 0
                ? Task.FromResult<IReadOnlyList<AccountSanction>>(Array.Empty<AccountSanction>())
                : Task.Run(() => _repository.History(name, limit));
        }

        /// <summary>The account and its characters, or null when no account has that name.</summary>
        public Task<AccountDetails?> Details(string account)
        {
            string name = (account ?? string.Empty).Trim();
            return name.Length == 0 ? Task.FromResult<AccountDetails?>(null) : Task.Run(() => _repository.Details(name));
        }

        /// <summary>The account's block when it may not log in now, or null. Blocking: call from a worker thread.</summary>
        public AccountBlock? FindBlock(string account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return null;

            IReadOnlyList<AccountBlock> blocked = _repository.Blocked([account.Trim()]);
            return blocked.Count == 0 ? null : blocked[0];
        }

        /// <summary>
        /// The account a GM or module names: <c>account:NAME</c>, <c>char:NAME</c>, or a bare name that is an account or a
        /// character's name. A bare name that is both, for different accounts, is refused as ambiguous.
        /// </summary>
        public Task<(string? Account, string Error)> Resolve(string text)
        {
            string value = (text ?? string.Empty).Trim();
            return Task.Run<(string?, string)>(() =>
            {
                if (TryPrefix(value, "account:", out string account))
                    return _repository.FindAccount(account) is string found ? (found, string.Empty) : (null, "No account named '" + account + "'.");
                if (TryPrefix(value, "char:", out string character))
                    return _repository.FindAccountOfCharacter(character) is string owner
                        ? (owner, string.Empty)
                        : (null, "No character named '" + character + "'.");
                if (value.Length == 0)
                    return (null, "An account or character name is needed.");

                string? byAccount = _repository.FindAccount(value);
                string? byCharacter = _repository.FindAccountOfCharacter(value);
                if (byAccount != null && byCharacter != null && !string.Equals(byAccount, byCharacter, StringComparison.OrdinalIgnoreCase))
                    return (null, "'" + value + "' is an account and another account's character; use account:" + value + " or char:" + value + ".");

                string? resolved = byAccount ?? byCharacter;
                return resolved != null ? (resolved, string.Empty) : (null, "No account or character named '" + value + "'.");
            });
        }

        static bool TryPrefix(string value, string prefix, out string rest)
        {
            rest = string.Empty;
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            rest = value[prefix.Length..].Trim();
            return true;
        }

        /// <summary>"permanent", or the end time in UTC.</summary>
        public static string Describe(DateTime? endsAtUtc)
            => endsAtUtc == null ? "permanent" : endsAtUtc.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

        /// <summary>
        /// Parses a duration such as <c>30m</c>, <c>12h</c>, <c>7d</c>, <c>2w</c> or <c>1d12h</c> (s, m, h, d, w) into seconds.
        /// </summary>
        public static bool TryParseDuration(string text, out long seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            long number = 0;
            bool digits = false;
            foreach (char c in text.Trim().ToLowerInvariant())
            {
                if (c is >= '0' and <= '9')
                {
                    number = number * 10 + (c - '0');
                    digits = true;
                    if (number > MaxSuspensionSeconds)
                        return false;
                    continue;
                }

                long unit = c switch { 's' => 1, 'm' => 60, 'h' => 3600, 'd' => 86400, 'w' => 604800, _ => 0 };
                if (unit == 0 || !digits)
                    return false;
                seconds += number * unit;
                if (seconds > MaxSuspensionSeconds)
                    return false;
                number = 0;
                digits = false;
            }

            return !digits && seconds > 0;
        }

        /// <summary>Removes the account's characters in play on this instance, each told why on its own tick. Returns how many.</summary>
        int Remove(string account, DateTime? endsAtUtc, string reason)
        {
            string text = endsAtUtc == null
                ? "Your account has been banned."
                : "Your account has been suspended until " + Describe(endsAtUtc) + ".";
            if (!string.IsNullOrWhiteSpace(reason))
                text += " Reason: " + reason.Trim();

            int removed = 0;
            foreach (Player player in _playfields.Value.SnapshotPlayers())
            {
                if (player.Session is not { } session || player.Playfield is not Playfield playfield
                    || !string.Equals(session.AccountName, account, StringComparison.OrdinalIgnoreCase))
                    continue;

                removed++;
                playfield.DispatchPlayerProjection(player, () =>
                {
                    if (player.Playfield is not Playfield current || player.Session == null)
                        return;

                    player.Session.Send(new ChatTextMessage
                    {
                        Identity = player.Identity, Text = text, Unknown1 = 0, Unknown2 = 0, Unknown3 = 0
                    });
                    _logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "Account sanction removes character={0} account={1}", player.Identity.Instance, account));
                    current.GetRequiredService<SpawnService>().LogoutPlayer(player);
                });
            }

            return removed;
        }

        /// <summary>Removes characters of accounts sanctioned since they logged in, here or on another instance.</summary>
        void Sweep()
        {
            if (Interlocked.Exchange(ref _sweeping, 1) == 1)
                return;

            try
            {
                string[] accounts = _playfields.Value.SnapshotPlayers()
                    .Select(player => player.Session?.AccountName ?? string.Empty)
                    .Where(account => account.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (accounts.Length == 0)
                    return;

                DateTime now = DateTime.UtcNow;
                foreach (AccountBlock block in _repository.Blocked(accounts))
                {
                    // The newest active sanction says why; a legacy Flags ban has none.
                    AccountSanction? active = _repository.History(block.Account, 5).FirstOrDefault(x => x.IsActive(now));
                    Remove(block.Account, block.EndsAtUtc, active?.Reason ?? string.Empty);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Account sanction sweep failed.");
            }
            finally
            {
                Volatile.Write(ref _sweeping, 0);
            }
        }

        public void Dispose() => _sweep.Dispose();
    }
}
