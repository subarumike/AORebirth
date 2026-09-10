namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AORebirth.Database.Dao;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class ZoneHandoffStoreTests
{
    sealed class Fixture : IDisposable
    {
        public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "aorebirth-handoff-test-" + Guid.NewGuid().ToString("N"));
        public DateTime Now = new(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        public ZoneHandoffStore Store => new(DirectoryPath, () => Now);
        public string Account(int id) => id == 3 ? "accountB" : "accountA";
        public ZoneHandoffTicket Issue(int character = 1)
        { var account = Account(character); return Store.Issue(account, Store.BeginLogin(account), character); }
        public void Dispose() { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }
    }
    [TestMethod] public void NoHandoffAndUnknownTokensFailWithoutCreatingAuthorization()
    {
        using var f = new Fixture();
        Assert.IsFalse(f.Store.Claim(1, 0, 0, f.Account).Accepted);
        Assert.IsFalse(f.Store.Claim(1, 123, 456, f.Account).Accepted);
    }
    [TestMethod] public void FreshAuthorizationIsBoundToSelectedCharacterAndAccount()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsFalse(f.Store.Claim(2, t.Cookie1, t.Cookie2, f.Account).Accepted);
        Assert.IsFalse(f.Store.Claim(3, t.Cookie1, t.Cookie2, f.Account).Accepted);
        Assert.IsFalse(f.Store.Claim(1, t.Cookie1, t.Cookie2, _ => "accountB").Accepted);
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
    }
    [TestMethod] public void AccountBMayNotUseItsAuthorizationForAccountA()
    {
        using var f = new Fixture(); var t = f.Issue(3);
        Assert.IsFalse(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
        Assert.IsTrue(f.Store.Claim(3, t.Cookie1, t.Cookie2, f.Account).Accepted);
    }
    [TestMethod] public void ExpiryUsesInjectedClockAndCannotBeRevivedByFreshStore()
    {
        using var f = new Fixture(); var t = f.Issue(); f.Now = f.Now.AddSeconds(121);
        Assert.AreEqual("expired_handoff", f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Reason);
    }
    [TestMethod] public void ConsumedAuthorizationRemainsConsumedAfterRestart()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
        Assert.AreEqual("already_claimed", f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Reason);
    }
    [TestMethod] public void ExpiryBoundaryAndClockBeforeIssuanceReject()
    {
        using var f = new Fixture(); var t = f.Issue(); var issued = f.Now;
        f.Now = issued.AddTicks(-1);
        Assert.AreEqual("expired_handoff", f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Reason);
        f.Now = issued.AddSeconds(ZoneHandoffStore.LifetimeSeconds);
        Assert.AreEqual("expired_handoff", f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Reason);
    }
    [TestMethod] public void AccountLookupFailureCannotAuthorizeOrConsumeTicket()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.ThrowsExactly<IOException>(() => f.Store.Claim(1, t.Cookie1, t.Cookie2, _ => throw new IOException("test outage")));
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
    }
    [TestMethod] public void OutstandingUnexpiredAuthorizationSurvivesStoreRestart()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsTrue(new ZoneHandoffStore(f.DirectoryPath, () => f.Now).Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
    }
    [TestMethod] public void NewAuthenticatedLoginInvalidatesOldTicketsAndOldIssuers()
    {
        using var f = new Fixture(); var generation = f.Store.BeginLogin("accountA");
        var t = f.Store.Issue("accountA", generation, 1); f.Store.BeginLogin("accountA");
        Assert.AreEqual("stale_login", f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Reason);
        Assert.ThrowsException<InvalidOperationException>(() => f.Store.Issue("accountA", generation, 1));
    }
    [TestMethod] public void ConcurrentClaimsHaveExactlyOneWinner()
    {
        using var f = new Fixture(); var t = f.Issue(); var accepted = new bool[16];
        Parallel.For(0, accepted.Length, i => accepted[i] = f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
        Assert.AreEqual(1, accepted.Count(x => x));
    }
    [TestMethod] public void UnknownTokenCannotBurnLegitimateAuthorization()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsFalse(f.Store.Claim(1, t.Cookie1 ^ 1, t.Cookie2, f.Account).Accepted);
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
    }
    [TestMethod] public void ConsumedLoginRequiresExplicitServerRedirectAuthorization()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
        Assert.AreEqual("already_claimed", f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account, "127.0.0.1", 7501).Reason);
    }
    [TestMethod] public void RedirectAuthorizationIsEndpointBoundSingleUseAndDoesNotUseOriginalExpiry()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
        f.Now = f.Now.AddMinutes(10);
        Assert.IsTrue(f.Store.AuthorizeRedirect(1, t.Cookie1, t.Cookie2, "127.0.0.1", 7501).Accepted);
        Assert.AreEqual("redirect_target_mismatch", f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account, "127.0.0.2", 7501).Reason);
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account, "127.0.0.1", 7501).Accepted);
        Assert.AreEqual("already_claimed", f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account, "127.0.0.1", 7501).Reason);
    }
    [TestMethod] public void RedirectMayBeRearmedOnlyAfterPriorAllowanceIsConsumedOrExpired()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
        Assert.IsTrue(f.Store.AuthorizeRedirect(1, t.Cookie1, t.Cookie2, "127.0.0.1", 7501).Accepted);
        Assert.AreEqual("redirect_already_authorized", f.Store.AuthorizeRedirect(1, t.Cookie1, t.Cookie2, "127.0.0.1", 7501).Reason);
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account, "127.0.0.1", 7501).Accepted);
        Assert.IsTrue(f.Store.AuthorizeRedirect(1, t.Cookie1, t.Cookie2, "127.0.0.1", 7501).Accepted);
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account, "127.0.0.1", 7501).Accepted);
    }
    [TestMethod] public void ConcurrentRedirectClaimsHaveExactlyOneWinnerAcrossStoreInstances()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
        Assert.IsTrue(f.Store.AuthorizeRedirect(1, t.Cookie1, t.Cookie2, "127.0.0.1", 7501).Accepted);
        var accepted = new bool[16];
        Parallel.For(0, accepted.Length, i => accepted[i] = new ZoneHandoffStore(f.DirectoryPath, () => f.Now)
            .Claim(1, t.Cookie1, t.Cookie2, f.Account, "127.0.0.1", 7501).Accepted);
        Assert.AreEqual(1, accepted.Count(x => x));
    }
    [TestMethod] public void ExpiredRedirectAndUnclaimedLoginCannotBeAuthorized()
    {
        using var f = new Fixture(); var unclaimed = f.Issue();
        Assert.AreEqual("initial_not_claimed", f.Store.AuthorizeRedirect(1, unclaimed.Cookie1, unclaimed.Cookie2, "127.0.0.1", 7501).Reason);
        Assert.IsTrue(f.Store.Claim(1, unclaimed.Cookie1, unclaimed.Cookie2, f.Account).Accepted);
        Assert.IsTrue(f.Store.AuthorizeRedirect(1, unclaimed.Cookie1, unclaimed.Cookie2, "127.0.0.1", 7501).Accepted);
        f.Now = f.Now.AddSeconds(121);
        Assert.AreEqual("expired_redirect", f.Store.Claim(1, unclaimed.Cookie1, unclaimed.Cookie2, f.Account, "127.0.0.1", 7501).Reason);
    }
    [TestMethod] public void FreshAccountAuthenticationDoesNotBreakAnAlreadyAdmittedSessionsRedirect()
    {
        using var f = new Fixture(); var t = f.Issue();
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account).Accepted);
        f.Store.BeginLogin("accountA");
        Assert.IsTrue(f.Store.AuthorizeRedirect(1, t.Cookie1, t.Cookie2, "127.0.0.1", 7501).Accepted);
        Assert.IsTrue(f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account, "127.0.0.1", 7501).Accepted);
    }
    [TestMethod] public void CorruptStorageFailsClosed()
    {
        using var f = new Fixture(); var t = f.Issue();
        File.WriteAllBytes(Path.Combine(f.DirectoryPath, "character-1.bin"), new byte[] { 1 });
        Assert.ThrowsException<EndOfStreamException>(() => f.Store.Claim(1, t.Cookie1, t.Cookie2, f.Account));
    }
}
