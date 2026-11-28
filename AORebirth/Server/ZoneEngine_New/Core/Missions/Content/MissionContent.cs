namespace ZoneEngine_New.Core.Missions;

using System;
using System.IO;
using Utility.GameData.Missions;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Quests;
using ZoneEngine_New.Core.Quests.Dungeons;

/// <summary>Validated inputs shared by terminal rolling, offer projection and mission QL consumers.</summary>
internal sealed record MissionContent(MissionRollPolicy Policy, MissionLevelData Levels,
    MissionRollEvidenceCatalog Cohorts, MissionRewardEvidenceModel NumericRewards, MissionTextContent Text,
    MissionGeographyContent Geography, MissionOfferCatalog Offers, MissionDestinationCatalog Destinations)
{
    const string Id = "MissionRoll";
    const string OffersFile = "Missions/" + MissionOfferCatalog.FileName;
    const string PolicyFile = "Missions/RollPolicy.json";
    const string LevelRulesFile = "Missions/Source/MissionLevelRules.json";
    const string LevelsFile = "Missions/Source/MissionLevels.csv";
    const string CohortsFile = "Missions/RollMixes.json";
    const string RewardsFile = "Missions/RewardObservations.json";
    const string TextFile = "Missions/Text.json";
    const string GeographyFile = "Missions/Locations.json";
    const string PlacementsFile = "Missions/Destinations/MissionEntrancePlacements.json";
    const string DestinationsFile = "Missions/Destinations/MissionDestinations.json";
    const string ItemCatalog = "ItemTemplates";

    static readonly Lazy<ContentSlot<MissionContent>> Runtime = new(() =>
    {
        var provider = GameDataContentProvider.Runtime;
        RegisterCompatibility(provider);
        return Register(provider);
    });

    internal static ContentSnapshot<MissionContent> Shared => Runtime.Value.Current;

    internal static ContentSlot<MissionContent> Register(GameDataContentProvider provider)
        => provider.Register(new ContentModule<MissionContent>(Id, 1,
            [new(OffersFile), new(PolicyFile), new(LevelRulesFile), new(LevelsFile), new(CohortsFile),
             new(RewardsFile), new(TextFile), new(GeographyFile), new(PlacementsFile), new(DestinationsFile)],
            [], Load));

    static MissionContent Load(ContentInputs inputs)
    {
        T Parse<T>(string path, Func<ReadOnlyMemory<byte>, T> parse)
        {
            try { return parse(inputs.Required(path)); }
            catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
            { throw new InvalidDataException($"Invalid mission input '{path}': {error.Message}", error); }
        }

        MissionRollPolicy policy = Parse(PolicyFile, MissionRollPolicy.Parse);
        var rules = Parse(LevelRulesFile, bytes =>
            MissionContentJson.ParseImmutable<MissionLevelRules>(bytes));
        return new MissionContent(
            policy,
            Parse(LevelsFile, bytes => MissionLevelData.Parse(MissionContentJson.Decode(bytes), rules)),
            Parse(CohortsFile, MissionRollEvidenceCatalog.Parse),
            Parse(RewardsFile, MissionRewardEvidenceModel.Parse),
            Parse(TextFile, MissionTextContent.Parse),
            Parse(GeographyFile, MissionGeographyContent.Parse),
            Parse(OffersFile, bytes => MissionOfferCatalog.Parse(bytes.Span, inputs.GetFileHash(OffersFile), policy)),
            Parse(DestinationsFile, bytes =>
                MissionDestinationCatalog.Parse(inputs.Required(PlacementsFile).ToArray(), bytes.ToArray())));
    }

    internal static ContentSlot<MissionKeyPolicy> RegisterKeys(GameDataContentProvider provider, IItemTemplateCatalog items)
        => provider.Register(new ContentModule<MissionKeyPolicy>("MissionKeys", 1,
            [new(MissionKeyPolicy.RelativePath)], [ItemCatalog],
            inputs => MissionKeyPolicy.Parse(MissionContentJson.Decode(inputs.Required(MissionKeyPolicy.RelativePath)), items)));

    // These owners continue to use their existing loader and source policy.
    internal static void RegisterCompatibility(GameDataContentProvider provider)
    {
        provider.RegisterCompatibility(new(ItemCatalog, nameof(ItemTemplateCatalog) + " / " + nameof(GameDataStore), null,
            ["items.dat", "ItemBehavior.json", "ItemTemplates.json"], []));
        provider.RegisterCompatibility(new("NpcTemplates", "NpcTemplateCatalog", null, ["NpcTemplates.json"], []));
        DungeonGenerationContent.Register(provider);
        provider.RegisterCompatibility(new("TerminalItemRewards", nameof(MissionRollRewardItems), null,
            ["XML Data/MissionRewards"], [Id, ItemCatalog]));
        provider.RegisterCompatibility(new("AuthoredMissions", nameof(AuthoredQuestCatalog), null,
            ["Content/Runtime/interactions.json", QuestCatalog.FileName], [ItemCatalog, "NpcTemplates", "QuestRewards"]));
        provider.RegisterCompatibility(new("QuestRewards", nameof(QuestRewardTable), null,
            [QuestRewardTable.FileName], [ItemCatalog]));
        provider.RegisterCompatibility(new("MissionChestLoot", nameof(MissionChestRKAllowlist), null,
            [MissionChestRKAllowlist.RelativePath, MissionChestLootExclusions.SeasonalPath, MissionChestLootExclusions.SpecialPath],
            [ItemCatalog, "MissionDungeons"]));
    }
}
