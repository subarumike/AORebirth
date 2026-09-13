namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Mobs;

[TestClass]
public sealed class NpcContentIntegrationTests
{
    [TestMethod]
    [DataRow(false, false, 100)]
    [DataRow(true, false, 200)]
    [DataRow(false, true, 300)]
    [DataRow(true, true, 300)]
    public void Composition_is_family_then_optional_overlay_then_individual(bool overlay, bool individual, int expected)
    {
        var family = new NpcFamilyStatTemplate(700, "fixture", new() { [27] = Curve(100), [100] = Curve(50) });
        var extra = overlay ? new NpcStatTemplate(800, "fixture", new() { [27] = Curve(200), [101] = Curve(60) }) : null;
        var npc = new MobTemplate { Hash = "TEST", MinLevel = 10, Stats = individual ? new() { [27] = 300 } : new() };
        var first = MobStatResolver.Resolve(npc, 10, family, extra);
        Assert.AreEqual(expected, first[27]); Assert.AreEqual(50, first[100]);
        if (overlay) Assert.AreEqual(60, first[101]);
        first[27] = -999;
        Assert.AreEqual(expected, MobStatResolver.Resolve(npc, 10, family, extra)[27]);
        Assert.AreEqual(100, family.Curves[27].Sample(10));
        Assert.AreEqual(individual ? 300 : 0, npc.Stats.GetValueOrDefault(27));
    }

    [TestMethod]
    public void Missing_family_does_not_use_developer_default()
    {
        var catalog = NpcFamilyStatCatalog.Build(new() { [1] = new() { StatCurves = new() { [27] = new() { [1] = 1, [10] = 10 } } } }, _ => Assert.Fail());
        Assert.IsTrue(catalog.TryResolve(1, out _));
        Assert.IsFalse(catalog.TryResolve(138, out _));
    }

    [TestMethod]
    public void Unresolved_placeholder_cannot_become_combat_content_through_a_policy_typo()
    {
        var npc = new MobTemplate { Hash = "TEST", ContentAcceptance = new() { Policy = "ACCEPTED", IdentityResolved = true,
            CombatAccepted = true, UnresolvedPlaceholder = true, Attackable = true, CombatAiEnabled = true, Source = "fixture" } };
        Assert.IsFalse(NpcContentAcceptance.CanSpawn(npc));
        Assert.ThrowsExactly<InvalidOperationException>(() => NpcContentAcceptance.RequireSpawnable(npc));
        npc.ContentAcceptance.UnresolvedPlaceholder = false;
        Assert.IsTrue(NpcContentAcceptance.CanSpawn(npc));
        npc.ContentAcceptance.Blockers = ["missing loadout"];
        Assert.IsFalse(NpcContentAcceptance.CanSpawn(npc));
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
    public void Imported_content_preserves_unknown_fields_as_blockers_and_disables_fallback_combat()
    {
        var rows = JsonSerializer.Deserialize<List<MobTemplate>>(File.ReadAllText(Path.Combine(Root(), "AORebirth/GameData/MobTemplates.json")))!;
        var placeholder = rows.Single(r => r.Hash == "AAAA");
        Assert.IsNotNull(placeholder.ContentAcceptance);
        Assert.IsTrue(placeholder.ContentAcceptance.UnresolvedPlaceholder);
        Assert.IsFalse(placeholder.ContentAcceptance.Attackable);
        Assert.IsFalse(placeholder.ContentAcceptance.CombatAiEnabled);
        Assert.IsFalse(NpcContentAcceptance.CanSpawn(placeholder));
        foreach (var imported in rows.Where(r => r.ContentAcceptance != null))
        {
            Assert.IsFalse(NpcContentAcceptance.CanSpawn(imported));
            Assert.AreEqual(0, imported.Weapons.Count, "Alternatives must never become concurrent slots.");
        }
        foreach (string hash in new[] { "MENI", "VEAE", "STFO" })
            Assert.IsTrue(rows.Any(r => r.Hash == hash));
    }

    [TestMethod]
    public void Imported_npc_content_is_outside_generic_runtime_files()
    {
        string root = Root();
        using var matrix = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"docs/reports/SUBWAY_NPC_COMBAT_RECONCILIATION.json")));
        string[] names = matrix.RootElement.GetProperty("npcs").EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToArray();
        string[] files = ["Mobs/MobStatResolver.cs", "Mobs/NpcFamilyStatTemplates.cs", "Mobs/NpcStatTemplates.cs",
            "Mobs/NpcContentAcceptance.cs", "GameData/GameDataStore.NpcStats.cs", "Playfield/SpawnService.cs", "Nanos/NanoService.cs"];
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

    static NpcStatCurve Curve(int value) => NpcStatCurve.Create(new() { [10] = value })!;
    static string Root()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName,"AORebirth/GameData/MobTemplates.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository content root unavailable.");
    }
}
