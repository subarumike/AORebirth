namespace ZoneEngine_New.Core.DailyLogin;

using System;
using System.IO;
using System.Linq;
using AORebirth.Interfaces.Persistence.Characters;
using ZoneEngine_New.Core.Commands;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Logging;

/// <summary>Restores the captured native claim trigger; HTTP requests alone cannot award inventory.</summary>
public sealed class DailyLoginClaimBridge(DailyLoginRewardService rewards, IDailyLoginClaimDao dao,
    DailyLoginClaimFiles files, IZoneLogger logger)
{
    public void HandleClaim(Player player, string authenticatedAccount, DateTime utcNow)
    {
        string account = (authenticatedAccount ?? string.Empty).Trim().ToLowerInvariant();
        if (account.Length == 0 || player.Session is null)
            return;
        try
        {
            // Completed requests remain evidence, so projection repair must also work with an empty queue.
            PublishCurrentBoard(account, player.Identity.Instance, utcNow);
            var pending = files.Pending(account, player.Identity.Instance);
            if (pending.Count == 0)
                return;
            bool freshCommittedGrant = false;
            foreach (DailyLoginPendingClaim request in pending)
            {
                if (request.RequiresLegacyReplay && dao.FindDailyLoginClaim(account, player.Identity.Instance, request.ClaimToken) is null)
                    throw new InvalidDataException("A historical Daily Login receipt has no verified ledger owner for this character. Its original request and receipt were retained.");
                DailyLoginClaimSnapshotData? before = dao.LoadDailyLoginState(account, player.Identity.Instance);
                DailyLoginAccountState imported = before is null
                    ? files.ReadLegacyProgress(account, player.Identity.Instance, utcNow, dao.HasDailyLoginHistory(account))
                    : DailyLoginAccountState.Parse(before.StateJson);
                DailyLoginClaimResult result = rewards.Claim(player, account, request.ClaimToken, imported, utcNow);
                // Receipts are durable projections only. A file failure is recovered by replaying the DB token.
                // Publish the durable board before exposing a committed result to a browser that immediately syncs it.
                PublishCurrentBoard(account, player.Identity.Instance, utcNow);
                files.WriteResult(result);
                if (!(result.Ok && result.Replayed) && !(freshCommittedGrant && result.AlreadyClaimedToday))
                    Feedback(result.Message);
                if (result.Ok && result.Committed && !result.Replayed)
                    freshCommittedGrant = true;
                if (player.IsPersistenceQuarantined)
                    break;
            }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Daily Login claim failed for character " + player.Identity.Instance + ". Pending requests were retained.");
            if (!player.IsPersistenceQuarantined)
                Feedback("Daily reward confirmation is unavailable. Your request has been retained; reopen Daily Login to retry.");
        }

        void Feedback(string message)
        {
            if (player.Session is not null)
                GmCommandFeedback.Send(player.Session, player, message);
        }
    }

    public bool PublishCurrentBoard(string account, int characterId, DateTime utcNow)
    {
        DailyLoginClaimSnapshotData? committed = dao.LoadDailyLoginState(account, characterId);
        if (committed is null)
            return false;
        if (committed.CharacterId != characterId || !string.Equals(committed.AccountUsername, account, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Daily Login board ownership does not match this character.");
        DailyLoginAccountState state = DailyLoginAccountState.Parse(committed.StateJson);
        if (state.CharacterId > 0 && state.CharacterId != characterId ||
            !string.IsNullOrWhiteSpace(state.AccountKey) && !string.Equals(state.AccountKey, account, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Daily Login calendar ownership conflicts with its verified ledger owner.");
        // The index migration qualifies old sole-owner ledger state; a filename alone never does.
        state.CharacterId = committed.CharacterId;
        state.AccountKey = account;
        files.PublishBoard(account, characterId, state, utcNow);
        return true;
    }
}

public sealed record DailyLoginBridgePaths(string[] ClaimsRoots, string RewardsJson, string ProfessionNanosJson)
{
    public static DailyLoginBridgePaths Resolve()
    {
        string[] configuredRoots = Configured("AO_REBIRTH_DAILY_LOGIN_CLAIMS_ROOTS");
        bool isolatedRoots = Environment.GetEnvironmentVariable("AO_REBIRTH_DAILY_LOGIN_ISOLATED_ROOTS") == "1";
        // Retain the original combined roots: an added configured bridge must not hide old account progress.
        var candidates = configuredRoots.AsEnumerable();
        if (!isolatedRoots)
        {
            string state = Environment.GetEnvironmentVariable("AO_REBIRTH_ZONE_STATE_DIR") ?? string.Empty;
            candidates = candidates
                .Concat(!string.IsNullOrWhiteSpace(state) ? [Path.Combine(state.Trim(), "daily-login", "claims")] : Array.Empty<string>())
                .Concat(OperatingSystem.IsWindows()
                    ? [@"C:\xampp\htdocs\daily\data\claims", @"C:\xampp\htdocs\uwg.daily.icc-rk\data\claims"] : Array.Empty<string>());
        }

        string[] roots = candidates.Distinct(StringComparer.Ordinal).ToArray();
        if (roots.Length == 0)
        {
            if (isolatedRoots)
                throw new InvalidOperationException("Isolated Daily Login claims require AO_REBIRTH_DAILY_LOGIN_CLAIMS_ROOTS.");

            roots = [Path.Combine(AppContext.BaseDirectory, "Content", "Daily", "data", "claims")];
        }
        string[] rewardPaths = Configured("AO_REBIRTH_DAILY_LOGIN_REWARDS_JSON");
        if (rewardPaths.Length == 0)
            rewardPaths = [Path.Combine(AppContext.BaseDirectory, "Content", "Daily", "rewards.json")];
        string reward = rewardPaths.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Daily Login rewards.json is unavailable; configure the existing AO_REBIRTH_DAILY_LOGIN_REWARDS_JSON setting.");
        return new DailyLoginBridgePaths(roots, reward,
            Path.Combine(AppContext.BaseDirectory, "Content", "Daily", "day10-profession-nanos.json"));
    }

    static string[] Configured(string variable)
        => (Environment.GetEnvironmentVariable(variable) ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
