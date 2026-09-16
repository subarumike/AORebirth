namespace ZoneEngine_New.Tests;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utility.Config;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class LocalityRuleDataTests
{
    [TestMethod]
    public void Reload_changes_defaults_and_invalid_override_is_rejected_without_repair()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"VisibilityNeighborLevel\":4,\"HotNeighborLevel\":1,\"WarmNeighborLevel\":3,\"CellSleepTime\":20,\"SpawnRate\":2}");
            var first = LocalityPolicy.Load(path);
            File.WriteAllText(path, "{\"VisibilityNeighborLevel\":5,\"HotNeighborLevel\":2,\"WarmNeighborLevel\":4,\"CellSleepTime\":40,\"SpawnRate\":3}");
            var second = LocalityPolicy.Load(path);
            Assert.AreEqual(2, first.SpawnRate); Assert.AreEqual(3, second.SpawnRate);
            Assert.AreEqual(20, first.CellSleepTimeSeconds); Assert.AreEqual(40, second.CellSleepTimeSeconds);
            Assert.ThrowsExactly<InvalidDataException>(() => LocalityPolicy.Load(path, new LocalitySettings { HotNeighborLevel = 6 }));
            Assert.ThrowsExactly<InvalidDataException>(() => LocalityPolicy.Load(path, new LocalitySettings { SpawnRate = -1 }));
        }
        finally { File.Delete(path); }
    }
}
