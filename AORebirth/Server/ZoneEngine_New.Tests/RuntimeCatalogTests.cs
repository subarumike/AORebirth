namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;

[TestClass]
public sealed class RuntimeCatalogTests
{
    [TestMethod]
    public void LegacyPvpTitlesResolveTheirTextCategoryAndAuthoredFallback()
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("pvp-catalog-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, PvpLegacyTitleCatalog.FileName), """
                {
                  "TextCategory": 42,
                  "Bands": [
                    { "MinRating": 20, "TextId": null, "Fallback": "Second" },
                    { "MinRating": 10, "TextId": 7, "Fallback": "" }
                  ]
                }
                """);
            File.WriteAllText(Path.Combine(root.FullName, GameDataPaths.TextFileName), """
                [
                  { "category": 41, "id": 7, "text": "Different category" },
                  { "category": 42, "id": 7, "text": "First" }
                ]
                """);

            PvpLegacyTitleCatalog catalog = Load(root.FullName);
            Assert.AreEqual(2, catalog.Count);
            Assert.AreEqual(string.Empty, catalog.TitleFor(9));
            Assert.AreEqual("First", catalog.TitleFor(10));
            Assert.AreEqual("First", catalog.TitleFor(19));
            Assert.AreEqual("Second", catalog.TitleFor(20));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void StagedLegacyPvpCatalogLoadsEveryAuthoredBand()
    {
        string root = StagedRoot();

        using JsonDocument policy = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, PvpLegacyTitleCatalog.FileName)));
        using JsonDocument text = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, GameDataPaths.TextFileName)));
        int category = policy.RootElement.GetProperty("TextCategory").GetInt32();
        var textById = text.RootElement.EnumerateArray()
            .Where(row => row.GetProperty("category").GetInt32() == category)
            .ToDictionary(row => row.GetProperty("id").GetInt32(), row => row.GetProperty("text").GetString());
        JsonElement[] bands = policy.RootElement.GetProperty("Bands").EnumerateArray().ToArray();

        PvpLegacyTitleCatalog catalog = Load(root);
        Assert.AreEqual(bands.Length, catalog.Count);
        int lowestRating = bands.Min(band => band.GetProperty("MinRating").GetInt32());
        if (lowestRating > 0)
            Assert.AreEqual(string.Empty, catalog.TitleFor(lowestRating - 1));

        foreach (JsonElement band in bands)
        {
            int rating = band.GetProperty("MinRating").GetInt32();
            JsonElement textId = band.GetProperty("TextId");
            string? expected = textId.ValueKind == JsonValueKind.Null
                ? band.GetProperty("Fallback").GetString()
                : textById.GetValueOrDefault(textId.GetInt32());
            Assert.IsFalse(string.IsNullOrWhiteSpace(expected), $"Rating {rating} needs an authored title.");
            Assert.AreEqual(expected, catalog.TitleFor(rating));
        }
    }

    [TestMethod]
    public void StagedEncounterCatalogsLoadAndResolveReferences()
    {
        string root = StagedRoot();
        var logger = new StubLogger();
        var data = new GameDataStore(logger, teleportDestinations: null, gameDataRoot: root);
        var items = new ItemTemplateCatalog(data, logger);
        Assert.IsNotNull(NpcPetSummonProfile.LoadOptional(root));
        MonsterCombatRulesCatalog.For(root).ValidateEncounterReferences(items, data);
    }

    [TestMethod]
    public void StagedStealthMultiplierCatalogLoadsAndUsesShippedSkillBands()
    {
        string root = StagedRoot();
        StealthMultiplierTable.Validate(root);
        using JsonDocument data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, StealthMultiplierTable.FileName)));
        var bands = data.RootElement.GetProperty("rows").EnumerateArray().ToDictionary(
            row => row.GetProperty("skill").GetInt32(),
            row => row.GetProperty("weights").EnumerateArray().Select(weight => weight.GetDouble()).ToArray());

        // Sample the middle of every nonzero interval in each authored row.
        foreach ((int skill, double[] weights) in bands)
        {
            double total = weights.Sum();
            double lower = 0;
            for (int multiplier = 1; multiplier <= weights.Length; multiplier++)
            {
                double weight = weights[multiplier - 1];
                if (weight > 0)
                {
                    double sample = (lower + weight / 2) / total;
                    Assert.AreEqual(multiplier, StealthMultiplierTable.Roll(root, skill, sample),
                        $"The shipped row for skill {skill} assigns this sample to {multiplier}x.");
                }
                lower += weight;
            }
        }

        int first = bands.Keys.Min();
        int last = bands.Keys.Max();
        double firstOneX = bands[first][0] / bands[first].Sum();
        double inFirstTwoX = (1 + firstOneX) / 2;
        Assert.AreEqual(1, StealthMultiplierTable.Roll(root, first - 1, inFirstTwoX));
        Assert.AreEqual(2, StealthMultiplierTable.Roll(root, first, inFirstTwoX));
        foreach (double sample in new[] { 0, 0.5, 0.99 })
            Assert.AreEqual(StealthMultiplierTable.Roll(root, last, sample), StealthMultiplierTable.Roll(root, last + 500, sample));

        // At 950, the one-times weight lies halfway between the authored 900 and 1000 rows.
        double midpointWeight = bands[900][0] / 2;
        Assert.AreEqual(1, StealthMultiplierTable.Roll(root, 950, midpointWeight - 0.001));
        Assert.AreEqual(2, StealthMultiplierTable.Roll(root, 950, midpointWeight + 0.001));

        Assert.AreEqual(0.0, bands[1000][0]);
        Assert.IsTrue(bands[1000][1] > 0);
        Assert.AreEqual(2, StealthMultiplierTable.Roll(root, 1000, 0));
        double sampleInThreeX = (bands[1000][1] + bands[1000][2] / 2) / bands[1000].Sum();
        Assert.AreEqual(3, StealthMultiplierTable.Roll(root, 1000, sampleInThreeX));
    }

    internal static string StagedRoot()
    {
        string? root = Environment.GetEnvironmentVariable("AO_REBIRTH_RUNTIME_CATALOG_VALIDATION_ROOT");
        if (string.IsNullOrEmpty(root))
            Assert.Inconclusive("The authoritative catalog staging root was not selected.");
        Assert.IsTrue(Path.IsPathFullyQualified(root!));
        Assert.IsTrue(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(GameDataPaths.OverrideEnvironmentVariableName)));
        return root!;
    }

    static PvpLegacyTitleCatalog Load(string root)
        => new(new StubGameData(HashItemCatalog.Parse("{}"), rootPath: root), new StubLogger());
}
