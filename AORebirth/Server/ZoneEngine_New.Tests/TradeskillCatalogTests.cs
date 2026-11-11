namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.Tradeskills;

[TestClass]
public sealed class TradeskillCatalogTests
{
    const string Json = """
        { "Recipes": [
          { "Key": "150922>42620", "Source": [150922], "Target": [42620, 42619], "AnyOrder": true,
            "Result": { "LowId": 150923, "HighId": 150924 }, "ConsumeTarget": true },
          { "Key": "1>2", "Source": [1], "Target": [2], "Result": { "LowId": 3, "HighId": 3 }, "ResultQl": "Fixed", "FixedQl": 1 },
          { "Key": "1>2b", "Source": [1], "Target": [2], "Result": { "LowId": 4, "HighId": 4 } }
        ] }
        """;

    [TestMethod]
    public void Recipe_matches_by_low_or_high_id_in_its_own_order()
    {
        TradeskillCatalog catalog = TradeskillCatalog.Parse(Json);
        Assert.IsTrue(catalog.TryMatch(150922, 150922, 42620, 42620, out TradeskillRecipe recipe, out bool swapped));
        Assert.AreEqual("150922>42620", recipe.Key);
        Assert.IsFalse(swapped);
        Assert.IsTrue(catalog.TryMatch(150922, 150922, 42619, 42619, out _, out _), "the target's high id matches too");
    }

    [TestMethod]
    public void Any_order_recipe_matches_reversed_and_others_do_not()
    {
        TradeskillCatalog catalog = TradeskillCatalog.Parse(Json);
        Assert.IsTrue(catalog.TryMatch(42620, 42619, 150922, 150922, out TradeskillRecipe recipe, out bool swapped));
        Assert.AreEqual("150922>42620", recipe.Key);
        Assert.IsTrue(swapped);
        Assert.IsFalse(catalog.TryMatch(2, 2, 1, 1, out _, out _), "1>2 is one-way");
    }

    [TestMethod]
    public void Earlier_recipe_wins_and_result_ql_parses()
    {
        TradeskillCatalog catalog = TradeskillCatalog.Parse(Json);
        Assert.IsTrue(catalog.TryMatch(1, 1, 2, 2, out TradeskillRecipe recipe, out _));
        Assert.AreEqual(3, recipe.Result.LowId);
        Assert.AreEqual(TradeskillResultQl.Fixed, recipe.ResultQl);
        Assert.IsTrue(catalog.CanBeSource(1, 1));
        Assert.IsTrue(catalog.CanBeTarget(150922, 150922), "an any-order source can be a target");
    }

    [TestMethod]
    public void JensenCutsRubiKaPearlWithShippedRecipe()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AI_START_HERE.md")))
            root = root.Parent;
        if (root == null)
            throw new DirectoryNotFoundException("The tradeskill tests must run from this repository's build output.");

        string path = Path.Combine(root.FullName, "AORebirth", "GameData", "Tradeskills.json");
        TradeskillCatalog catalog = TradeskillCatalog.Parse(File.ReadAllText(path));
        Assert.IsTrue(catalog.TryMatch(151366, 151366, 136646, 136647, out TradeskillRecipe recipe, out bool swapped),
            "The shipped catalog must let Jensen Gem Cutter cut Pearl of Rubi-Ka.");
        Assert.IsFalse(swapped);
        Assert.IsFalse(catalog.TryMatch(136646, 136647, 151366, 151366, out _, out _));
        Assert.AreEqual((151423, 151424), (recipe.Result.LowId, recipe.Result.HighId));
        Assert.AreEqual(TradeskillResultQl.Target, recipe.ResultQl);
        Assert.IsFalse(recipe.ConsumeSource);
        Assert.IsTrue(recipe.ConsumeTarget);
        Assert.AreEqual(0, recipe.QlRangePercent);
        CollectionAssert.AreEqual(new[] { (125, 500, 0), (17, 190, 0) },
            recipe.Skills.ConvertAll(skill => (skill.Stat, skill.Percent, skill.PerBump)).ToArray());
    }
}
