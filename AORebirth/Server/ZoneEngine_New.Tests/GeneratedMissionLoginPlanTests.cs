namespace ZoneEngine_New.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Missions;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class GeneratedMissionLoginPlanTests
{
    [TestMethod]
    public void ActiveOwnedMissionPreservesExactSavedPositionAndHydratedInventory()
    {
        var original = Original();
        var result = ZoneLoginHandler.ApplyMissionLoginPlan(original, new(0x160001, null, null, false));
        Assert.AreSame(original, result);
        Assert.AreEqual(12f, result.Character.X);
    }

    [TestMethod]
    public void EndedMissionReturnsToFrozenExteriorWithoutMutatingRepositorySnapshot()
    {
        var original = Original();
        var result = ZoneLoginHandler.ApplyMissionLoginPlan(original, new(735, new Vector3(100, 20, 300), null, true));
        Assert.AreNotSame(original, result);
        Assert.AreEqual(0x160001, original.Character.Playfield);
        Assert.AreEqual(735, result.Character.Playfield); Assert.AreEqual(100f, result.Character.X);
        Assert.AreEqual(20f, result.Character.Y); Assert.AreEqual(300f, result.Character.Z);
        Assert.AreEqual(original.Character.Id, result.Character.Id); Assert.AreEqual("Owner", result.Character.Name);
        Assert.AreEqual(original.Character.HeadingW, result.Character.HeadingW);
        Assert.AreSame(original.Items, result.Items); Assert.AreSame(original.Stats, result.Stats);
        Assert.AreSame(original.UploadedNanoIds, result.UploadedNanoIds);
    }

    static CharacterHydrationResult Original() => new()
    {
        Character = new() { Id = 17, Name = "Owner", Playfield = 0x160001, X = 12, Y = 3, Z = 9, HeadingW = 1 },
        Items = new ItemInstanceRecord[1], Stats = new StatRecord[1], UploadedNanoIds = new[] { 100 }
    };
}
