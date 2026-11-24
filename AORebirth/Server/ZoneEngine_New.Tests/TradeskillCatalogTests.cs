namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;

using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
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

    const int CutterId = 151366;
    const int RawPearlLowId = 136646;
    const int RawPearlHighId = 136647;
    const int CutPearlLowId = 151423;
    const int CutPearlHighId = 151424;

    static Identity ToolSlot => new() { Type = IdentityType.Inventory, Instance = 64 };

    static Identity InputSlot => new() { Type = IdentityType.Inventory, Instance = 65 };

    static Identity NextInputSlot => new() { Type = IdentityType.Inventory, Instance = 66 };

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
    [DataRow(RawPearlLowId)]
    [DataRow(RawPearlHighId)]
    public void CanonicalJensenRecipeLoadsAndMatchesEachRubiKaPearlEndpoint(int pearlId)
    {
        TradeskillCatalog catalog = LoadAuthoritativeCatalog();
        Assert.IsTrue(catalog.TryMatch(CutterId, CutterId, pearlId, pearlId, out TradeskillRecipe recipe, out bool swapped),
            "The authoritative catalog must let Jensen Gem Cutter cut this Pearl of Rubi-Ka endpoint.");
        Assert.IsFalse(swapped);
        Assert.IsFalse(catalog.TryMatch(pearlId, pearlId, CutterId, CutterId, out _, out _));
        Assert.AreEqual((CutPearlLowId, CutPearlHighId), (recipe.Result.LowId, recipe.Result.HighId));
        Assert.AreEqual(TradeskillResultQl.Target, recipe.ResultQl);
        Assert.IsFalse(recipe.ConsumeSource);
        Assert.IsTrue(recipe.ConsumeTarget);
        Assert.AreEqual(0, recipe.QlRangePercent);
        Assert.AreEqual(0, recipe.MinTargetQl);
        Assert.AreEqual(0, recipe.MaxBump);
        Assert.IsFalse(recipe.Implant);
        CollectionAssert.AreEquivalent(
            new[] { (CharacterStat.MechanicalEngineering, 500, 0), (CharacterStat.Agility, 190, 0) },
            recipe.Skills.Select(skill => ((CharacterStat)skill.Stat, skill.Percent, skill.PerBump)).ToArray());
    }

    [TestMethod]
    [DataRow(RawPearlLowId, 1, CutPearlLowId, 5, 2)]
    [DataRow(RawPearlLowId, 17, CutPearlLowId, 85, 33)]
    [DataRow(RawPearlHighId, 200, CutPearlHighId, 1000, 380)]
    public void CutterKeepsPearlQualityAndCanSafelyCutAnotherPearl(
        int inputId, int quality, int resultTemplate, int engineering, int agility)
    {
        using var world = new InventoryActionTests.World();
        (TradeskillService service, Item tool, Item input) = Prepare(world, inputId, quality, engineering, agility);
        InventoryMutationBatch? batch = null;
        world.Persistence.BeforeCommit = mutation => batch = mutation;

        Assert.AreEqual(TradeskillService.CombineOutcome.Combined, service.TryCombineDrag(world.Player, ToolSlot, InputSlot));
        Container inventory = world.Player.Inventory.Inventory;
        Assert.AreSame(tool, inventory.Content[ToolSlot.Instance]);
        Item result = inventory.Content[InputSlot.Instance];
        Assert.AreEqual((CutPearlLowId, CutPearlHighId, quality, resultTemplate),
            (result.LowId, result.HighId, result.Quality, result.Definition.Id));
        Assert.IsTrue(result.IsPersisted);
        Assert.AreEqual(ToolSlot.Instance, world.Persistence.Rows[tool.InstanceId].ContainerPlacement);
        Assert.AreEqual((int)IdentityType.None, world.Persistence.Rows[input.InstanceId].ContainerType);
        Assert.AreEqual((int)IdentityType.Inventory, world.Persistence.Rows[result.InstanceId].ContainerType);
        Assert.AreEqual(quality, world.Persistence.Rows[result.InstanceId].Quality);

        Assert.IsNotNull(batch);
        Assert.AreEqual(input.InstanceId, batch.Locations.Single().InstanceId);
        Assert.AreEqual(result.InstanceId, batch.Inserts.Single().InstanceId);
        Assert.AreEqual(TradeskillService.CombineOutcome.NoRecipe, service.TryCombineDrag(world.Player, ToolSlot, InputSlot));
        Assert.AreSame(result, inventory.Content[InputSlot.Instance]);
        Assert.AreEqual(1, world.Persistence.Calls);
        Assert.AreEqual(1, world.Session.Messages.OfType<TemplateActionMessage>().Count());

        Item nextPearl = TestWorld.CreateItem(lowId: inputId, highId: inputId, quality: quality, instanceId: 3);
        AddIngredient(world, nextPearl, NextInputSlot);
        Assert.AreEqual(TradeskillService.CombineOutcome.Combined, service.TryCombineDrag(world.Player, ToolSlot, NextInputSlot));
        Assert.AreSame(tool, inventory.Content[ToolSlot.Instance]);
        Assert.AreSame(result, inventory.Content[InputSlot.Instance]);
        Item nextResult = inventory.Content[NextInputSlot.Instance];
        Assert.AreNotEqual(result.InstanceId, nextResult.InstanceId);
        Assert.AreEqual((CutPearlLowId, CutPearlHighId, quality, resultTemplate),
            (nextResult.LowId, nextResult.HighId, nextResult.Quality, nextResult.Definition.Id));
        Assert.AreEqual((int)IdentityType.None, world.Persistence.Rows[nextPearl.InstanceId].ContainerType);
        Assert.AreEqual(NextInputSlot.Instance, world.Persistence.Rows[nextResult.InstanceId].ContainerPlacement);
        Assert.AreEqual(2, world.Persistence.Calls);
        Assert.AreEqual(2, world.Session.Messages.OfType<TemplateActionMessage>().Count());
    }

    [TestMethod]
    [DataRow(84, 33)]
    [DataRow(85, 32)]
    public void CombineRequiresBothSkillsAndRoundsFractionalRequirementUp(int engineering, int agility)
    {
        using var world = new InventoryActionTests.World();
        (TradeskillService service, Item tool, Item input) = Prepare(world, RawPearlLowId, 17, engineering, agility);

        Assert.AreEqual(TradeskillService.CombineOutcome.Refused, service.TryCombineDrag(world.Player, ToolSlot, InputSlot));
        Assert.AreEqual(0, world.Persistence.Calls);
        Assert.AreEqual(0, world.Ids.Calls);
        AssertInputsUnchanged(world, tool, input);
    }

    [TestMethod]
    public void CombinePersistenceFailureLeavesBothIngredientsAndSendsNoResult()
    {
        using var world = new InventoryActionTests.World();
        (TradeskillService service, Item tool, Item input) = Prepare(world, RawPearlLowId, 17, 85, 33);
        world.Persistence.Failure = new InvalidOperationException("Injected inventory persistence failure.");

        Assert.AreEqual(TradeskillService.CombineOutcome.Refused, service.TryCombineDrag(world.Player, ToolSlot, InputSlot));
        Assert.AreEqual(1, world.Persistence.Calls);
        AssertInputsUnchanged(world, tool, input);
    }

    static (TradeskillService Service, Item Tool, Item Input) Prepare(
        InventoryActionTests.World world, int inputId, int quality, int engineering, int agility)
    {
        world.Player.Stats.Set(CharacterStat.MechanicalEngineering, engineering);
        world.Player.Stats.Set(CharacterStat.Agility, agility);
        Item tool = TestWorld.CreateItem(lowId: CutterId, highId: CutterId, quality: 10, instanceId: 1);
        Item input = TestWorld.CreateItem(lowId: inputId, highId: inputId, quality: quality, instanceId: 2);
        AddIngredient(world, tool, ToolSlot);
        AddIngredient(world, input, InputSlot);

        var templates = new StubCatalog().Add(CutPearlLowId, 1).Add(CutPearlHighId, 200);
        var logger = new StubLogger();
        var builder = new ItemBuilder(templates, world.Ids, logger);
        var data = new StubGameData(HashItemCatalog.Parse("{}"), rootPath: AppContext.BaseDirectory);
        return (new TradeskillService(LoadAuthoritativeCatalog(), world.Actions, builder, templates, logger, data), tool, input);
    }

    static TradeskillCatalog LoadAuthoritativeCatalog()
    {
        string? root = Environment.GetEnvironmentVariable("AO_REBIRTH_RUNTIME_CATALOG_VALIDATION_ROOT");
        if (string.IsNullOrEmpty(root))
            Assert.Inconclusive("The authoritative catalog staging root was not selected.");
        Assert.IsTrue(Path.IsPathFullyQualified(root!));
        Assert.IsTrue(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName)));
        return TradeskillCatalog.Load(root!);
    }

    static void AddIngredient(InventoryActionTests.World world, Item item, Identity slot)
    {
        Container inventory = world.Player.Inventory.Inventory;
        Assert.IsTrue(inventory.Add(slot.Instance, item));
        world.Persistence.Rows.Add(item.InstanceId, item.ToRecord(inventory.Identity, slot.Instance, item.StackCount));
    }

    static void AssertInputsUnchanged(InventoryActionTests.World world, Item tool, Item input)
    {
        Container inventory = world.Player.Inventory.Inventory;
        Assert.AreEqual(2, inventory.Content.Count);
        Assert.AreSame(tool, inventory.Content[ToolSlot.Instance]);
        Assert.AreSame(input, inventory.Content[InputSlot.Instance]);
        Assert.AreEqual(2, world.Persistence.Rows.Count);
        Assert.AreEqual((int)IdentityType.Inventory, world.Persistence.Rows[tool.InstanceId].ContainerType);
        Assert.AreEqual((int)IdentityType.Inventory, world.Persistence.Rows[input.InstanceId].ContainerType);
        Assert.AreEqual(0, world.Session.Messages.OfType<TemplateActionMessage>().Count());
    }
}
