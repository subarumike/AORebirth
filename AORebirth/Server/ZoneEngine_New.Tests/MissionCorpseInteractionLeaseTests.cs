namespace ZoneEngine_New.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Missions;

[TestClass]
public sealed class MissionCorpseInteractionLeaseTests
{
    [TestMethod]
    public void DelayedClaimAndAcknowledgementRequireExactSessionWorldAndLiveOwner()
    {
        object session = new(), world = new();
        var lease = new MissionCorpseInteractionLease(session, world, 77, 1000);
        Assert.IsTrue(lease.Matches(session, world, 77, false, false, 8, 999));
        Assert.IsFalse(lease.Matches(new object(), world, 77, false, false, 1, 999), "Reconnect cannot inherit a pending corpse action.");
        Assert.IsFalse(lease.Matches(session, new object(), 77, false, false, 1, 999));
        Assert.IsFalse(lease.Matches(session, world, 78, false, false, 1, 999));
        Assert.IsFalse(lease.Matches(session, world, 77, true, false, 1, 999));
        Assert.IsFalse(lease.Matches(session, world, 77, false, true, 1, 999));
        Assert.IsFalse(lease.Matches(session, world, 77, false, false, 8.01, 999));
        Assert.IsFalse(lease.Matches(session, world, 77, false, false, double.NaN, 999));
        Assert.IsFalse(lease.Matches(session, world, 77, false, false, 1, 1000));
    }
}
