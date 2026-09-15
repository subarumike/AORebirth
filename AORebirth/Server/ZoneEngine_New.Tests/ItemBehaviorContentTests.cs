namespace ZoneEngine_New.Tests;

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Helpers;
using SmokeLounge.AOtomation.Messaging.GameData;

[TestClass]
public sealed class ItemBehaviorContentTests
{
    const string Fixture = """
        {"ProtectedItems":[10],"Packages":[{"SourceIds":[20],"ProductLowId":30,"ProductHighId":31,"Quality":7,"Unique":true}],
        "VitalItems":[{"TemplateIds":[40],"Consumed":true,"LockStat":"FirstAid","LockSeconds":10,"MinimumQuality":1,"MaximumQuality":10,"MinimumRestore":5,"MaximumRestore":50}]}
        """;

    [TestMethod]
    public void PackageAndVitalBindingsReloadWithoutRecompile()
    {
        var original = ItemBehaviorContent.Parse(Fixture);
        var changed = ItemBehaviorContent.Parse(Fixture.Replace("\"ProductLowId\":30", "\"ProductLowId\":300")
            .Replace("\"MaximumRestore\":50", "\"MaximumRestore\":100"));
        var item = new Item { LowId = 20, HighId = 20 };
        Assert.AreEqual(30, original.FindPackage(item)!.ProductLowId);
        Assert.AreEqual(300, changed.FindPackage(item)!.ProductLowId);
        Assert.AreEqual(50, original.FindVitalItem(new Item { LowId = 40, HighId = 40 })!.RestoreAt(10));
        Assert.AreEqual(100, changed.FindVitalItem(new Item { LowId = 40, HighId = 40 })!.RestoreAt(10));
        Assert.IsTrue(original.IsProtected(new Item { LowId = 10, HighId = 10 }));
        Assert.IsFalse(original.IsProtected(item));
    }

    [TestMethod]
    public void DuplicatePackageBindingFailsValidation()
        => Assert.ThrowsExactly<InvalidDataException>(() => ItemBehaviorContent.Parse(Fixture.Replace("[20]", "[20,20]")));

    [TestMethod]
    public void FistAssignmentChangesFromContentWithSameBinary()
    {
        const string source = """{"FistWeapons":[{"Tier":1,"LowId":100,"HighId":101}]}""";
        var first = MartialArtsFistResolver.Resolve(Profession.MartialArtist, 300, ItemBehaviorContent.Parse(source));
        var second = MartialArtsFistResolver.Resolve(Profession.MartialArtist, 300,
            ItemBehaviorContent.Parse(source.Replace("\"LowId\":100", "\"LowId\":200")));
        Assert.AreEqual(100, first.LowId); Assert.AreEqual(200, second.LowId);
        Assert.AreEqual(first.Quality, second.Quality);
    }
}
