namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;

[TestClass]
public sealed class EditableRuleDataTests
{
    [TestMethod]
    public void ResourceCoefficientsCanChangeWithoutRebuildingOrChangingPriorSnapshot()
    {
        string path = Path.GetTempFileName();
        try
        {
            string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GameData", "CharacterRules.json"));
            File.WriteAllText(path, source);
            var before = CharacterRuleData.Load(path);
            int original = before.ComputeVital("health", 1, 1, 1, 1, 1);
            var json = JsonNode.Parse(source)!;
            var values = json["vitals"]!["health"]!["base"]!.AsArray();
            values[0] = values[0]!.GetValue<int>() + 17;
            File.WriteAllText(path, json.ToJsonString());
            var after = CharacterRuleData.Load(path);
            Assert.AreEqual(original + 17, after.ComputeVital("health", 1, 1, 1, 1, 1));
            Assert.AreEqual(original, before.ComputeVital("health", 1, 1, 1, 1, 1));
            Assert.AreSame(before.GetType().Assembly, after.GetType().Assembly);
            json["vitals"]!["health"]!["base"] = null;
            File.WriteAllText(path, json.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => CharacterRuleData.Load(path));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void ProgressionRejectsAmbiguityAndUsesEditedBandsWithoutRebuilding()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"startingIp\":10,\"bands\":[{\"start\":1,\"title\":1,\"ipPerLevel\":5}]}");
            var before = ProgressionData.Load(path);
            File.WriteAllText(path, "{\"startingIp\":10,\"bands\":[{\"start\":1,\"title\":2,\"ipPerLevel\":9}]}");
            var after = ProgressionData.Load(path);
            Assert.AreEqual(20, before.TotalIp(3));
            Assert.AreEqual(28, after.TotalIp(3));
            Assert.AreEqual(1, before.TitleFor(3));
            Assert.AreEqual(2, after.TitleFor(3));
            foreach (string invalid in new[]
            {
                "{\"startingIp\":10,\"startingIp\":20,\"bands\":[]}",
                "{\"startingIp\":10,\"bands\":[null]}",
                "{\"startingIp\":10,\"bands\":[{\"start\":2,\"title\":1,\"ipPerLevel\":5}]}"
            })
            {
                File.WriteAllText(path, invalid);
                Assert.ThrowsExactly<InvalidDataException>(() => ProgressionData.Load(path));
            }
        }
        finally { File.Delete(path); }
    }
}
