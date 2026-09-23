namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Mobs;

[TestClass]
public sealed class NpcContentIntegrationTests
{
    [TestMethod]
    public void Unresolved_placeholder_cannot_become_combat_content_through_a_policy_typo()
    {
        var npc = new MobTemplate { Hash = "TEST", Name = "Fixture", UnresolvedPlaceholder = true };
        Assert.IsFalse(NpcTemplateValidation.CanSpawn(npc));
        Assert.ThrowsExactly<InvalidOperationException>(() => NpcTemplateValidation.RequireSpawnable(npc));
        npc.Attackable = false;
        Assert.IsTrue(NpcTemplateValidation.CanSpawn(npc));
        npc.UnresolvedPlaceholder = false;
        npc.Attackable = true;
        Assert.IsTrue(NpcTemplateValidation.CanSpawn(npc));
    }

    [TestMethod]
    public void Weapon_quality_variants_are_selected_as_alternatives_and_conflicts_fail_closed()
    {
        var low = new NpcWeaponVariant { LowId = 10, HighId = 19, Qualities = [18, 19], Evidence = ["fixture"] };
        var boundary = new NpcWeaponVariant { LowId = 19, HighId = 19, Qualities = [20], Evidence = ["fixture"] };
        var high = new NpcWeaponVariant { LowId = 21, HighId = 30, Qualities = [21], Evidence = ["fixture"] };
        Assert.AreSame(low, NpcWeaponVariant.RequireUnique([low, boundary, high], 19));
        Assert.AreSame(boundary, NpcWeaponVariant.RequireUnique([low, boundary, high], 20));
        Assert.AreSame(high, NpcWeaponVariant.RequireUnique([low, boundary, high], 21));
        Assert.ThrowsExactly<InvalidOperationException>(() => NpcWeaponVariant.RequireUnique([low, high], 20));
        high.Qualities = [20];
        Assert.ThrowsExactly<InvalidOperationException>(() => NpcWeaponVariant.RequireUnique([boundary, high], 20));
    }

    [TestMethod]
    public void Imported_npc_content_is_outside_generic_runtime_files()
    {
        string root = Root();
        using var matrix = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"docs/reports/SUBWAY_NPC_COMBAT_RECONCILIATION.json")));
        string[] names = matrix.RootElement.GetProperty("npcs").EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToArray();
        string[] files = ["Mobs/NpcTemplateValidation.cs", "Playfield/SpawnService.cs", "Nanos/NanoRuntime.cs"];
        foreach (string file in files)
        {
            string source = File.ReadAllText(Path.Combine(root,"AORebirth/Server/ZoneEngine_New/Core",file));
            foreach (string name in names) Assert.IsFalse(source.Contains('"'+name+'"', StringComparison.Ordinal), file);
            foreach (var npc in matrix.RootElement.GetProperty("npcs").EnumerateArray())
            {
                string hash = npc.GetProperty("hash").GetString()!;
                Assert.IsFalse(source.Contains('"'+hash+'"', StringComparison.Ordinal), file);
                int monster = npc.GetProperty("appearance").GetProperty("monsterData").GetInt32();
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(source, @"\b"+monster+@"\b"), file);
            }
        }
    }

    static string Root()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName,"AORebirth/GameData/ItemTemplates.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository content root unavailable.");
    }
}
