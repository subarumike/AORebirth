namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Accounts;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// <c>.account suspend|ban|unban|history</c>: account suspensions and bans. The target is an account or character name
    /// (<c>account:NAME</c> / <c>char:NAME</c> when a name is both). Database work runs on a worker thread and the answer
    /// comes back on the GM's own tick. Banning and unbanning need <see cref="BanGmLevel"/>; the command itself
    /// (suspend, history) needs <see cref="RequiredGmLevel"/>.
    /// </summary>
    public sealed class AccountCommand(AccountSanctionService sanctions) : IGmCommand
    {
        /// <summary>The GM level <c>ban</c> and <c>unban</c> need, above the command's own.</summary>
        public const int BanGmLevel = 1000;

        const int HistoryShown = 15;

        public string Name => "account";

        public int RequiredGmLevel => 100;

        public string Usage => ".account info <target> | suspend <target> <duration> [reason] | ban <target> [reason] | unban <target> [reason] | history <target>";

        public GmCommandHelp Help { get; } = new(
            "Shows account details, suspends, bans and unbans accounts, and shows their sanction history.",
            [
                new(".account info <target>", "The account, its status and its characters."),
                new(".account suspend <target> <duration> [reason]", "Suspends the account for a time and removes its characters in play."),
                new(".account ban <target> [reason]", "Bans the account permanently (GM level 1000)."),
                new(".account unban <target> [reason]", "Lifts every active suspension and ban (GM level 1000)."),
                new(".account history <target>", "Whether the account can log in, and its sanctions, newest first."),
            ],
            [
                new(".account info Bob", "Details of the account owning Bob."),
                new(".account suspend Bob 3d Botting", "Suspend the account owning Bob for three days."),
                new(".account suspend account:bob123 12h Spam", "Suspend account bob123 for twelve hours."),
                new(".account ban Bob Gold selling", "Ban the account owning Bob."),
                new(".account unban Bob Appeal accepted", "Lift its sanctions."),
                new(".account history Bob", "Show its history."),
            ],
            "The target is an account or character name. Use account:NAME or char:NAME when a name is both. "
            + "Durations: s, m, h, d, w, combined like 1d12h (60 seconds to ten years). "
            + "Sanctions block login on every zone; characters in play are removed at once here and within a minute elsewhere.");

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string[] args = context.Args;
            string action = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;
            switch (action)
            {
                case "suspend":
                    Suspend(context, args);
                    return;
                case "ban":
                case "unban":
                    if (context.Player.Stats.GetOrZero(CharacterStat.GmLevel) < BanGmLevel)
                    {
                        GmCommandFeedback.SendError(context.Session, context.Player, ".account " + action + " needs GM level " + BanGmLevel + ".");
                        return;
                    }

                    if (args.Length < 2)
                        break;
                    if (action == "ban")
                        Ban(context, args);
                    else
                        Unban(context, args);
                    return;
                case "history":
                    if (args.Length != 2)
                        break;
                    History(context, args[1]);
                    return;
                case "info":
                    if (args.Length != 2)
                        break;
                    Info(context, args[1]);
                    return;
            }

            context.SendUsage("Usage: " + Usage);
        }

        void Suspend(GmCommandContext context, string[] args)
        {
            if (args.Length < 3 || !AccountSanctionService.TryParseDuration(args[2], out long seconds)
                || seconds < AccountSanctionService.MinSuspensionSeconds)
            {
                context.SendUsage("Usage: .account suspend <target> <duration> [reason] (60s to ten years, e.g. 30m, 12h, 7d)");
                return;
            }

            string reason = Rest(args, 3);
            string issuer = Issuer(context.Player);
            Run(context, args[1], async account =>
            {
                SanctionIssueResult result = await sanctions.Issue(account, seconds, issuer, reason).ConfigureAwait(false);
                return [result.Ok ? Issued(result) : result.Error];
            });
        }

        void Ban(GmCommandContext context, string[] args)
        {
            string reason = Rest(args, 2);
            string issuer = Issuer(context.Player);
            Run(context, args[1], async account =>
            {
                SanctionIssueResult result = await sanctions.Issue(account, null, issuer, reason).ConfigureAwait(false);
                return [result.Ok ? Issued(result) : result.Error];
            });
        }

        void Unban(GmCommandContext context, string[] args)
        {
            string reason = Rest(args, 2);
            string issuer = Issuer(context.Player);
            Run(context, args[1], async account =>
            {
                SanctionLift lift = await sanctions.Lift(account, issuer, reason).ConfigureAwait(false);
                string line = lift.Lifted == 0 && !lift.LegacyFlagsCleared
                    ? account + " has no active suspension or ban."
                    : string.Format(CultureInfo.InvariantCulture, "{0}: {1} sanction(s) lifted{2}.", account, lift.Lifted,
                        lift.LegacyFlagsCleared ? " and the old login ban cleared" : string.Empty);
                return [line];
            });
        }

        void History(GmCommandContext context, string target)
        {
            Run(context, target, async account =>
            {
                IReadOnlyList<AccountSanction> history = await sanctions.History(account, HistoryShown).ConfigureAwait(false);
                AccountBlock? block = await Task.Run(() => sanctions.FindBlock(account)).ConfigureAwait(false);
                var lines = new List<string>
                {
                    account + ": " + (block == null ? "can log in." : "blocked until " + AccountSanctionService.Describe(block.Value.EndsAtUtc) + ".")
                };
                if (history.Count == 0)
                    lines.Add("No sanctions recorded.");

                DateTime now = DateTime.UtcNow;
                foreach (AccountSanction sanction in history)
                {
                    string state = sanction.LiftedAtUtc != null
                        ? "lifted " + AccountSanctionService.Describe(sanction.LiftedAtUtc) + " by " + sanction.LiftedBy
                        : sanction.IsActive(now) ? "ACTIVE" : "ended";
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "#{0} {1} {2} by {3}, until {4} [{5}]{6}",
                        sanction.Id, sanction.Permanent ? "ban" : "suspension",
                        sanction.IssuedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), sanction.IssuedBy,
                        AccountSanctionService.Describe(sanction.EndsAtUtc), state,
                        string.IsNullOrEmpty(sanction.Reason) ? string.Empty : ": " + sanction.Reason));
                }

                return lines;
            });
        }

        void Info(GmCommandContext context, string target)
        {
            Run(context, target, async account =>
            {
                AccountDetails? details = await sanctions.Details(account).ConfigureAwait(false);
                if (details == null)
                    return ["No account named '" + account + "'."];

                AccountBlock? block = await Task.Run(() => sanctions.FindBlock(details.Account)).ConfigureAwait(false);
                var lines = new List<string>
                {
                    string.Format(CultureInfo.InvariantCulture, "Account {0} (id {1}), created {2}", details.Account, details.Id,
                        details.Created.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    "Email: " + MaskEmail(details.Email),
                    string.Format(CultureInfo.InvariantCulture, "GM level {0}, expansions {1}, character slots {2}", details.GmLevel,
                        details.Expansions, details.AllowedCharacters),
                    "Status: " + (block == null
                        ? "can log in"
                        : (block.Value.EndsAtUtc == null ? "banned" : "suspended until " + AccountSanctionService.Describe(block.Value.EndsAtUtc))
                            + (details.Flags != 0 ? " (login.Flags " + details.Flags.ToString(CultureInfo.InvariantCulture) + ")" : string.Empty)),
                    string.Format(CultureInfo.InvariantCulture, "Characters ({0}):", details.Characters.Count)
                };
                foreach (AccountCharacter character in details.Characters)
                {
                    string profession = Enum.IsDefined(typeof(Profession), character.Profession)
                        ? ((Profession)character.Profession).ToString()
                        : "profession " + character.Profession.ToString(CultureInfo.InvariantCulture);
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "  {0} (id {1}) level {2} {3}, {4} playfield {5}",
                        character.Name, character.Id, character.Level, profession, character.Online ? "ONLINE in" : "last in",
                        character.PlayfieldId));
                }

                return lines;
            });
        }

        /// <summary>The first letter and the domain: enough to recognise an address without showing it whole.</summary>
        static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "(none)";
            int at = email.IndexOf('@');
            return at <= 0 ? "***" : email[0] + "***" + email[at..];
        }

        static string Issuer(Player gm)
            => "gm:" + (string.IsNullOrEmpty(gm.Name) ? gm.Identity.Instance.ToString(CultureInfo.InvariantCulture) : gm.Name);

        static string Rest(string[] args, int from) => string.Join(' ', args.Skip(from)).Trim();

        static string Issued(SanctionIssueResult result)
        {
            AccountSanction sanction = result.Sanction!;
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} (sanction #{2}, until {3}); {4} character(s) removed here.",
                sanction.Account, sanction.Permanent ? "banned" : "suspended", sanction.Id,
                AccountSanctionService.Describe(sanction.EndsAtUtc), result.Removed);
        }

        /// <summary>Resolves <paramref name="target"/> and runs <paramref name="work"/> on it off the tick; its lines go back to the GM.</summary>
        void Run(GmCommandContext context, string target, Func<string, Task<IEnumerable<string>>> work)
        {
            Player gm = context.Player;
            IZoneSession session = context.Session;
            _ = Task.Run(async () =>
            {
                IEnumerable<string> lines;
                try
                {
                    (string? account, string error) = await sanctions.Resolve(target).ConfigureAwait(false);
                    lines = account == null ? [error] : await work(account).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    lines = ["Command failed: " + exception.Message];
                }

                gm.Playfield?.DispatchPlayerProjection(gm, () =>
                {
                    if (ReferenceEquals(gm.Session, session))
                        GmCommandFeedback.SendLines(session, gm, lines);
                });
            });
        }
    }
}
