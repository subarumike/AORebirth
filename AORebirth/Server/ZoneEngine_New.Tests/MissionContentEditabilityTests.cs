namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using AORebirth.Core.Playfields;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Inventory;

[TestClass]
public sealed class MissionContentEditabilityTests
{
    [TestMethod]
    public void WeaponPoolAndNpcStatsChangeWithSameBinary()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "GameData", "Missions", "NpcContent.json");
        var first = MissionNpcContent.Parse(File.ReadAllText(path));
        var fixture = JsonNode.Parse(File.ReadAllText(path))!;
        fixture["Weapons"] = JsonNode.Parse("""
            [{"LowId":87654,"HighId":87655,"MaximumQuality":11,"Stats":{"Flags":67109889,"MultipleCount":1,"Energy":4294967295,"AttackDelay":235,"RechargeDelay":235}}]
            """);
        fixture["Stats"]!["RunSpeed"] = 901;
        var second = MissionNpcContent.Parse(fixture.ToJsonString());
        var oldWeapon = NativeWeapon(first, new StubCatalog().AddWeapon(121570, 23));
        var newWeapon = NativeWeapon(second, new StubCatalog().AddWeapon(87654, 11).AddWeapon(87655, 11));
        Assert.IsTrue(oldWeapon.IsWieldableCombatWeapon()); Assert.IsTrue(newWeapon.IsWieldableCombatWeapon());
        Assert.AreEqual(121570, oldWeapon!.LowId);
        Assert.AreEqual(87654, newWeapon!.LowId); Assert.AreEqual(87655, newWeapon.HighId); Assert.AreEqual(11, newWeapon.Quality);
        Assert.AreEqual(901, second.Stats[CharacterStat.RunSpeed]);
        Assert.AreNotEqual(first.Stats[CharacterStat.RunSpeed], second.Stats[CharacterStat.RunSpeed]);
    }

    [TestMethod]
    public void ArtifactPoolReloadUsesEditedDataWithSameBinary()
    {
        string root = Path.Combine(Path.GetTempPath(), "mission-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(AppContext.BaseDirectory, "GameData", "Missions", "Artifacts.json");
            string destination = Path.Combine(root, "Artifacts.json");
            var document = JsonNode.Parse(File.ReadAllText(source))!;
            File.WriteAllText(destination, document.ToJsonString());
            var first = MissionContentJson.Read<MissionArtifactContent>(root, "Artifacts.json");
            document["RepairPool"]![0]!["LowId"] = 87654;
            document["RepairPool"]![0]!["HighId"] = 87655;
            File.WriteAllText(destination, document.ToJsonString());
            var second = MissionContentJson.Read<MissionArtifactContent>(root, "Artifacts.json");
            Assert.AreNotEqual(first.RepairPool[0].LowId, second.RepairPool[0].LowId);
            var originalItem = first.SelectRepairComponent(id => id == first.RepairPool[0].LowId, new Random(1));
            var changedItem = second.SelectRepairComponent(id => id is 87654 or 87655, new Random(1));
            Assert.AreNotEqual(originalItem.LowId, changedItem.LowId);
            Assert.AreEqual(87654, changedItem.LowId); Assert.AreEqual(87655, changedItem.HighId);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void MissionLayoutsLoadWithoutProvenancePermission()
    {
        string root = Path.Combine(Path.GetTempPath(), "mission-layouts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var document = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GameData", "Missions", "Layouts.json")))!;
            void RemoveProvenance(JsonNode? value)
            {
                if (value is JsonObject obj)
                    foreach (var property in obj.ToArray())
                    {
                        if (string.Equals(property.Key, "Provenance", StringComparison.OrdinalIgnoreCase)) obj[property.Key] = new JsonArray();
                        else RemoveProvenance(property.Value);
                    }
                else if (value is JsonArray array) foreach (var child in array) RemoveProvenance(child);
            }
            RemoveProvenance(document);
            File.WriteAllText(Path.Combine(root, "Layouts.json"), document.ToJsonString());
            var bundles = MissionContentJson.Read<MissionAcgLayoutBundle[]>(root, "Layouts.json");
            var catalog = MissionAcgLayoutCatalogLoader.Load(bundles, []);
            Assert.AreEqual(5, catalog.Layouts.Count);
            Assert.IsTrue(bundles.All(bundle => bundle.Provenance.Count == 0));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void AmbiguousMissionWeaponPoolIsRejected()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GameData", "Missions", "NpcContent.json")))!;
        var weapons = document["Weapons"]!.AsArray();
        weapons.Add(weapons[0]!.DeepClone());
        Assert.ThrowsExactly<InvalidDataException>(() => MissionNpcContent.Parse(document.ToJsonString()));
    }

    [TestMethod]
    public void WeaponAssignmentDoesNotRequireEvidenceMetadata()
    {
        var variant = new NpcWeaponVariant { LowId = 21, HighId = 22, Qualities = [11] };
        Assert.AreSame(variant, NpcWeaponVariant.RequireUnique([variant], 11));
    }

    [TestMethod]
    public void NativeCombatUsesTemplateStatsWithoutCapturedAttackMetadata()
    {
        var content = MissionNpcContent.Load();
        content.Provenance = [];
        content.Ranged.Id = string.Empty;
        content.Ranged.FirstHitDelay = double.NaN;
        content.Ranged.HitType = -1;
        var catalog = new StubCatalog().AddWeapon(121570, 23);
        catalog.Require(121570).Stats[CharacterStat.AttackDelay] = 137;
        var item = NativeWeapon(content, catalog);
        Assert.AreEqual(137, item.GetStat(CharacterStat.AttackDelay));
        Assert.IsTrue(item.IsWieldableCombatWeapon());
    }

    [TestMethod]
    public void NativeCombatRejectsConfiguredNonWeaponTemplate()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            NativeWeapon(MissionNpcContent.Load(), new StubCatalog().Add(121570, 23)));
    }

    static Item NativeWeapon(MissionNpcContent content, StubCatalog catalog)
        => GeneratedMissionNpcFactory.CreateCombatWeapon(1_000_001, 20, true,
            new ItemBuilder(catalog, new StubLogger()), catalog, content);
}
