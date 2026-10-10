namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility.GameData.Missions;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Missions;

[TestClass]
[DoNotParallelize]
public sealed class MissionDestinationIntegrationTests
{
    static readonly Identity Owner = new() { Type = IdentityType.CanbeAffected, Instance = 990101 };
    static readonly byte[] SupportedSliders = [156, 255, 255, 255, 255, 255];
    static readonly string[] MissionTypes = ["KILL_PERSON", "FIND_PERSON", "FIND_ITEM", "REPAIR", "RETURN_ITEM"];
    const uint TerminalInstance = 3221226127u;
    static MissionDestinationCatalog catalog = null!;
    static string? previousGameDataRoot;
    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void Initialize(TestContext context)
    {
        _ = context;
        string root = FindRepositoryRoot();
        string gameDataRoot = Path.Combine(root, "AORebirth", "GameData");
        previousGameDataRoot = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, gameDataRoot);
        catalog = MissionDestinationCatalog.Load(gameDataRoot, requireObservedSelection: true);
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void Cleanup() => Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, previousGameDataRoot);

    [TestMethod]
    public void CatalogPreservesFullAndObservedSetsCompleteIdentityExactCoordinatesAndNames()
    {
        Assert.AreEqual(2242, catalog.Count);
        Assert.AreEqual(812, catalog.ObservedDestinations.Count);
        Assert.AreEqual(1430, catalog.Placements.Count(value => !catalog.IsObservedRandomMissionDestination(value.Identity)));
        Assert.IsFalse(catalog.DestinationUniquenessWithinCohortRequired);
        foreach (var placement in catalog.Placements)
        {
            Assert.AreSame(placement, catalog.GetByIdentity(placement.IdentityType, placement.IdentityInstance));
            Assert.IsTrue(catalog.TryGetByExactWorldPos(placement.PlayfieldId,
                placement.LocalXBits, placement.LocalYBits, placement.LocalZBits, out var byCoordinates));
            Assert.AreSame(placement, byCoordinates);
            Assert.AreEqual(Encoding.Latin1.GetString(Convert.FromHexString(placement.RawNameHex)), placement.DisplayName);
        }
        var captured = catalog.GetByIdentity(0xDAC6, 0xC00001F9);
        Assert.AreEqual("Central Desert Den", captured.DisplayName);
        Assert.AreEqual(505, captured.PlayfieldId);
        Assert.AreEqual(1150418278u, captured.LocalXBits);
        Assert.AreEqual(1088391123u, captured.LocalYBits);
        Assert.AreEqual(1157902054u, captured.LocalZBits);
        Assert.IsFalse(catalog.TryGetByIdentity(0xDAC5, captured.IdentityInstance, out _));
        Assert.IsFalse(catalog.TryGetByExactWorldPos(captured.PlayfieldId,
            captured.LocalXBits, captured.LocalYBits + 1, captured.LocalZBits, out _));
        Assert.AreEqual("\u00c6nima HQ", catalog.GetByIdentity(0xDAC6, 0xC0000280).DisplayName);
    }

    [TestMethod]
    public void SelectionUsesCapturedJointConditionAndObservedSubsetOnly()
    {
        Assert.IsTrue(catalog.HasObservedSelection);
        Assert.AreEqual(547, catalog.ObservedConditionCount);
        Assert.AreEqual(812, catalog.ObservedWorldPositionCount);
        foreach (string type in MissionTypes)
        {
            Assert.IsTrue(catalog.TryGetObservedDestinations(Condition(type), out var candidates), type);
            Assert.IsTrue(candidates.Count > 0 && candidates.Count < 812);
            Assert.AreEqual(candidates.Count, candidates.Select(value => value.Identity).Distinct().Count());
            foreach (var placement in candidates)
            {
                Assert.IsTrue(catalog.IsObservedRandomMissionDestination(placement.Identity));
                Assert.IsTrue(catalog.TryGetWorldPosition(placement.Identity, out var worldPosition));
                Assert.AreEqual(placement.Identity, worldPosition.Identity);
            }
        }
    }

    [TestMethod]
    public void GeneratedOffersPreserveSelectedIdentityExactWorldPositionAndFrozenProjection()
    {
        var request = Request();
        int next = 100000;
        var response = GeneratedMissionRollService.Generate(request, Owner, 2, 655, 0, 0,
            MissionLocationSide.Omni, 1201445827, 17, 4567, () => next++, catalog, 1, 15, out var selected);
        Assert.AreEqual(5, selected.Count);
        CollectionAssert.AreEqual(Enumerable.Range(100000, 5).ToArray(), response.QuestInfos.Select(value => value.QuestIdentity.Instance).ToArray());
        Assert.AreEqual(100005, next);
        DateTime issued = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        var batch = GeneratedMissionRollProjection.Create(request, response, 655, 2, 17, 4567, issued, selected);
        byte[] wire = GeneratedMissionWire.Write(response);
        var decoded = GeneratedMissionWire.Read(wire);
        for (int i = 0; i < selected.Count; i++)
        {
            var placement = catalog.GetByIdentity(selected[i].IdentityType, selected[i].IdentityInstance);
            Assert.IsTrue(catalog.TryGetWorldPosition(selected[i], out var worldPosition));
            var action = response.QuestInfos[i].QuestActions.Single();
            var decodedAction = decoded.QuestInfos[i].QuestActions.Single();
            var row = batch.Offers[i];
            Assert.AreEqual(placement.PlayfieldId, action.Playfield.Instance);
            Assert.AreEqual(worldPosition.PlayfieldIdentityType, unchecked((uint)action.Playfield.Type));
            Assert.AreEqual(placement.LocalXBits, BitConverter.SingleToUInt32Bits(action.X));
            Assert.AreEqual(placement.LocalYBits, BitConverter.SingleToUInt32Bits(action.Y));
            Assert.AreEqual(placement.LocalZBits, BitConverter.SingleToUInt32Bits(action.Z));
            Assert.AreEqual(worldPosition.WorldOffsetX, action.Unknown18);
            Assert.AreEqual(worldPosition.WorldOffsetZ, action.Unknown19);
            Assert.AreEqual(action.Unknown18, decodedAction.Unknown18);
            Assert.AreEqual(action.Unknown19, decodedAction.Unknown19);
            Assert.AreEqual(placement.LocalXBits, BitConverter.SingleToUInt32Bits(decodedAction.X));
            Assert.AreEqual(placement.LocalYBits, BitConverter.SingleToUInt32Bits(decodedAction.Y));
            Assert.AreEqual(placement.LocalZBits, BitConverter.SingleToUInt32Bits(decodedAction.Z));
            Assert.AreEqual(selected[i].IdentityType, unchecked((uint)row.EntranceType));
            Assert.AreEqual(selected[i].IdentityInstance, unchecked((uint)row.EntranceInstance));
            Assert.IsTrue(row.EntranceInstance < 0);
            Assert.AreEqual(action.Unknown18, row.EntranceLow);
            Assert.AreEqual(action.Unknown19, row.EntranceHigh);
            Assert.AreEqual(action.Playfield.Instance, row.DestinationInstance);
            Assert.AreEqual(placement.LocalXBits, BitConverter.SingleToUInt32Bits(row.DestinationX));
            Assert.AreEqual(placement.LocalYBits, BitConverter.SingleToUInt32Bits(row.DestinationY));
            Assert.AreEqual(placement.LocalZBits, BitConverter.SingleToUInt32Bits(row.DestinationZ));
            Assert.AreEqual(1, row.Quality);
            Assert.IsTrue(catalog.TryGetObservedDestinations(Condition(MissionTypes[row.MissionType]), out var eligible));
            Assert.IsTrue(eligible.Any(value => value.Identity.Equals(selected[i])));
            CollectionAssert.AreEqual(wire, row.FrozenWireBody);
            Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(wire)), row.FrozenWireSha256);
        }
        var conflicting = selected.ToArray();
        conflicting[0] = new MissionPlacementIdentity(0, 0);
        Assert.ThrowsExactly<InvalidOperationException>(() => GeneratedMissionRollProjection.Create(request, response, 655, 2, 17, 4567, issued, conflicting));
    }

    [TestMethod]
    public void DestinationRepeatsArePermittedWithinOneDeterministicCohort()
    {
        const int seed = 19;
        int next = 100000;
        var response = GeneratedMissionRollService.Generate(Request(), Owner, 2, 655, 0, 0, MissionLocationSide.Omni,
            1201445827, seed, 4567, () => next++, catalog, 1, 15, out var selected);
        Assert.AreEqual(5, selected.Count);
        Assert.IsTrue(selected.Distinct().Count() < selected.Count);
        Assert.AreEqual(5, response.QuestInfos.Select(value => value.QuestIdentity.Instance).Distinct().Count());
        TestContext.WriteLine("DETERMINISTIC_DESTINATION_REPEAT_SEED=" + seed);
    }

    [TestMethod]
    public void MissingSelectionFilePreservesLookupsButFailsClosedForRuntimeRolling()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "aorebirth-mission-destination-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(temporaryRoot, "Missions", "Destinations");
        string source = Path.Combine(FindRepositoryRoot(), "AORebirth", "GameData", "Missions", "Destinations");
        string[] files = ["MissionEntrancePlacements.json", "ObservedMissionDestinations.json",
            "MissionDestinationCatalogManifest.json", "MissionDestinationSelection.json"];
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string file in files) File.Copy(Path.Combine(source, file), Path.Combine(directory, file));
            Assert.IsTrue(MissionDestinationCatalog.Load(temporaryRoot, requireObservedSelection: true).HasObservedSelection);
            File.Delete(Path.Combine(directory, "MissionDestinationSelection.json"));
            var lookupOnly = MissionDestinationCatalog.Load(temporaryRoot);
            Assert.AreEqual(2242, lookupOnly.Count);
            Assert.AreEqual(812, lookupOnly.ObservedDestinations.Count);
            Assert.IsFalse(lookupOnly.HasObservedSelection);
            Assert.IsFalse(lookupOnly.TryGetObservedDestinations(Condition("KILL_PERSON"), out _));
            Assert.ThrowsExactly<InvalidDataException>(() => MissionDestinationCatalog.Load(temporaryRoot, requireObservedSelection: true));
            int allocations = 0;
            Assert.ThrowsExactly<NotSupportedException>(() => GeneratedMissionRollService.Generate(Request(), Owner, 2,
                655, 0, 0, MissionLocationSide.Omni, 1201445827, 17, 4567, () => 100000 + allocations++, lookupOnly, 1, 15, out _));
            Assert.AreEqual(0, allocations);
        }
        finally
        {
            foreach (string file in files) File.Delete(Path.Combine(directory, file));
            Directory.Delete(directory);
            Directory.Delete(Path.Combine(temporaryRoot, "Missions"));
            Directory.Delete(temporaryRoot);
        }
    }

    [TestMethod]
    [DataRow("breed")]
    [DataRow("profession")]
    [DataRow("faction")]
    [DataRow("terminal-playfield")]
    [DataRow("terminal-type")]
    [DataRow("terminal-instance")]
    [DataRow("slider")]
    [DataRow("difficulty")]
    [DataRow("quality-over-200")]
    public void UnsupportedJointConditionsFailBeforeAllocatingAnyOfferIdentity(string changed)
    {
        var request = Request();
        int level = 2, breed = 1, profession = 15, terminalPlayfield = 655;
        var side = MissionLocationSide.Omni;
        switch (changed)
        {
            case "breed": breed = 2; break;
            case "profession": profession = 14; break;
            case "faction": side = MissionLocationSide.Clan; break;
            case "terminal-playfield": terminalPlayfield = 540; break;
            case "terminal-type": request.MissionTerminalIdentity = new() { Type = (IdentityType)56002, Instance = unchecked((int)TerminalInstance) }; break;
            case "terminal-instance": request.MissionTerminalIdentity = new() { Type = (IdentityType)56001, Instance = unchecked((int)(TerminalInstance + 1)) }; break;
            case "slider": request.GoodBadSlider = 0; break;
            case "difficulty": request.LevelSlider = 2; break;
            case "quality-over-200": level = 220; request.LevelSlider = 11; Assert.AreEqual(250, MissionLevelRuntime.GetMissionQuality(level, request.LevelSlider)); break;
        }
        int allocations = 0;
        Assert.ThrowsExactly<NotSupportedException>(() => GeneratedMissionRollService.Generate(request, Owner, level,
            terminalPlayfield, 0, 0, side, 1201445827, 17, 4567, () => 100000 + allocations++, catalog, breed, profession, out _));
        Assert.AreEqual(0, allocations);
    }

    [TestMethod]
    public void MarginalEvidenceDoesNotSupplyUncapturedJointQualityOrMissionType()
    {
        Assert.IsFalse(catalog.TryGetObservedDestinations(new MissionDestinationCondition(2, 2, 1, 2, 1, 15, 655,
            56001, TerminalInstance, SupportedSliders, "KILL_PERSON"), out _));
        Assert.IsFalse(catalog.TryGetObservedDestinations(Condition("UNRESOLVED"), out _));
    }

    static MissionDestinationCondition Condition(string type) => new(2, 1, 1, 2, 1, 15, 655, 56001,
        TerminalInstance, SupportedSliders, type);

    static QuestAlternativeMessage Request() => new()
    {
        Identity = Owner, VersionId = 4, LevelSlider = 1,
        MissionTerminalIdentity = new() { Type = (IdentityType)56001, Instance = unchecked((int)TerminalInstance) },
        GoodBadSlider = SupportedSliders[0], OrderChaosSlider = SupportedSliders[1], OpenHiddenSlider = SupportedSliders[2],
        PhysicalMysticalSlider = SupportedSliders[3], HeadOnStealthSlider = SupportedSliders[4], MoneyExperienceSlider = SupportedSliders[5],
        QuestInfos = []
    };

    static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AI_START_HERE.md"))) return directory.FullName;
        throw new DirectoryNotFoundException("The focused mission tests must run from this repository's build output.");
    }
}
