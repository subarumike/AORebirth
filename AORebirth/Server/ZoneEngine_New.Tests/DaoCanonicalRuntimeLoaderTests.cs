using System;
using System.IO;
using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.WorldSimulation;

namespace ZoneEngine_New.Tests;

[TestClass]
public sealed class DaoCanonicalRuntimeLoaderTests
{
    private static readonly NLogZoneLogger Logger = new();
    private static GameDataStore gameData = null!;
    private static ItemTemplateCatalog items = null!;

    [ClassInitialize]
    public static void LoadGameData(TestContext context)
    {
        string? root = Environment.GetEnvironmentVariable("DAO_CANONICAL_GAMEDATA_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("The DAO runtime check requires an explicit absolute GameData root.");

        gameData = new GameDataStore(Logger, null, root);
        items = new ItemTemplateCatalog(gameData, Logger);
    }

    [TestMethod]
    public void NpcPetPolicyResolvesAuthoredItemsAndNpcTemplates()
        => MonsterCombatRulesCatalog.For(gameData.RootPath).ValidatePetSummons(items, gameData);

    [TestMethod]
    public void SharedItemBehaviorResolvesCanonicalItemTemplates()
        => ItemBehaviorBindings.Load(Path.Combine(gameData.RootPath, "ItemBehavior.json"))
            .ValidateTemplateReferences(id => items.TryGet(id, out _));

    [TestMethod]
    public void BossModifierBindingsResolveCanonicalItemTemplates()
        => ItemBehaviorContent.Load(gameData.RootPath).ValidateBossModifierTemplates(items);

    [TestMethod]
    public void ReverseProxyExitDestinationsResolveCanonicalDoors()
        => ReverseProxyExitCatalog.Load(Path.Combine(gameData.RootPath, "TeleportRoutes.json"))
            .ValidateDestinationReferences((playfield, door) =>
                PortalDoorLandingResolver.TryResolveDoorLanding(gameData.GetPlayfieldGeometry(playfield), door, 0, out _, out _));
}
