namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using AORebirth.Core.GameData;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility.GameData.Missions;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
[DoNotParallelize]
public sealed class MissionDestinationIntegrationTests
{
    static readonly Identity Owner = new() { Type = IdentityType.CanbeAffected, Instance = 990101 };
    static readonly byte[] SupportedSliders = [156, 255, 255, 255, 255, 255];
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
        catalog = MissionDestinationCatalog.Load(gameDataRoot);
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void Cleanup() => Environment.SetEnvironmentVariable(GameDataPaths.EnvironmentVariableName, previousGameDataRoot);

    [TestMethod]
    public void CatalogPreservesFullAndObservedSetsCompleteIdentityExactCoordinatesAndNames()
    {
        Assert.AreEqual(2242, catalog.Count);
        Assert.AreEqual(812, catalog.ObservedDestinations.Count);
        Assert.AreEqual(1430, catalog.Placements.Count(value => !catalog.IsObservedRandomMissionDestination(value.Identity)));
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
    public void TwoRuntimeFilesPreserveTheCompleteCatalogAndObservedWorldPositions()
    {
        using var files = new TemporaryCatalog();
        CollectionAssert.AreEquivalent(new[] { "MissionEntrancePlacements.json", "MissionDestinations.json" },
            Directory.GetFiles(files.Directory).Select(Path.GetFileName).ToArray());
        var loaded = MissionDestinationCatalog.Load(files.Root);
        Assert.AreEqual(2242, loaded.Count);
        Assert.AreEqual(812, loaded.ObservedDestinations.Count);
        Assert.AreEqual(812, loaded.ObservedWorldPositionCount);
        Assert.AreEqual(files.Read("MissionDestinations.json")["Pools"]!.AsArray().Count, loaded.TerminalPoolCount);
        foreach (var placement in loaded.ObservedDestinations)
        {
            Assert.IsTrue(loaded.IsObservedRandomMissionDestination(placement.Identity));
            Assert.IsTrue(loaded.TryGetWorldPosition(placement.Identity, out var worldPosition));
            Assert.AreEqual(placement.Identity, worldPosition.Identity);
        }
    }

    [TestMethod]
    public void ExpectedQualityPoolsAreExactDistinctUnionsOfRetainedObservations()
    {
        var qualities = catalog.ExpectedMissionQls.Order().ToArray();
        Assert.AreEqual(45, qualities.Length);
        using var files = new TemporaryCatalog();
        var pools = files.Read("MissionDestinations.json")["Pools"]!.AsArray();
        foreach (int quality in qualities)
        {
            var expected = new HashSet<MissionPlacementIdentity>();
            foreach (var pool in pools.Where(value => value!["ExpectedMissionQl"]!.GetValue<int>() == quality))
            {
                int origin = pool!["TerminalPlayfieldId"]!.GetValue<int>();
                Assert.IsTrue(catalog.TryGetDestinations(origin, quality, out var observed, out bool fallback));
                Assert.IsFalse(fallback);
                var sourceIdentities = pool["DestinationIdentities"]!.AsArray().Select(ReadIdentity).ToHashSet();
                Assert.IsTrue(sourceIdentities.SetEquals(observed.Select(value => value.Identity)));
                expected.UnionWith(sourceIdentities);
            }
            Assert.IsTrue(catalog.TryGetObservedDestinations(quality, out var actual));
            Assert.AreEqual(expected.Count, actual.Count);
            Assert.IsTrue(expected.SetEquals(actual.Select(value => value.Identity)));
            Assert.IsTrue(actual.All(value => catalog.TryGetWorldPosition(value.Identity, out _)));
        }
        Assert.IsTrue(catalog.TryGetObservedDestinations(25, out var ql25));
        Assert.AreEqual(124, ql25.Count);
        Assert.IsTrue(catalog.TryGetObservedDestinations(35, out var ql35));
        Assert.AreEqual(184, ql35.Count);
        foreach (int quality in new[] { 0, 34, 36, 67, 200, 250 })
        {
            Assert.IsFalse(catalog.TryGetObservedDestinations(quality, out var absent));
            Assert.AreEqual(0, absent.Count);
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
            Assert.IsTrue(catalog.TryGetObservedDestinations(row.Quality, out var eligible));
            Assert.IsTrue(eligible.Any(value => value.Identity.Equals(selected[i])));
            CollectionAssert.AreEqual(wire, row.FrozenWireBody);
            Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(wire)), row.FrozenWireSha256);
        }
        var conflicting = selected.ToArray();
        conflicting[0] = new MissionPlacementIdentity(0, 0);
        Assert.ThrowsExactly<InvalidOperationException>(() => GeneratedMissionRollProjection.Create(request, response, 655, 2, 17, 4567, issued, conflicting));
    }

    [TestMethod]
    public void CapturedCenteredBorealisLevel35ConditionGeneratesFiveExactDestinationOffers()
    {
        var request = Request();
        request.LevelSlider = 6;
        request.MissionTerminalIdentity = new() { Type = (IdentityType)0xDAC1, Instance = unchecked((int)0xC0000320u) };
        request.GoodBadSlider = request.OrderChaosSlider = request.OpenHiddenSlider = 255;
        request.PhysicalMysticalSlider = request.HeadOnStealthSlider = request.MoneyExperienceSlider = 255;
        Assert.AreEqual(35, MissionLevelRuntime.GetMissionQuality(35, request.LevelSlider));
        TestContext.WriteLine("CAPTURED_REQUEST level=35 ql=35 detent=6 side=2 breed=3 profession=12 terminal=800/0xDAC1:0xC0000320 secondary=255,255,255,255,255,255");
        Assert.IsTrue(catalog.TryGetDestinations(800, 35, out var candidates, out bool fallback));
        Assert.IsFalse(fallback);
        int next = 100000;
        var response = GeneratedMissionRollService.Generate(request, Owner, 35, 800, 0, 0,
            MissionLocationSide.Omni, 1201445827, 17, 4567, () => next++, catalog, 3, 12, out var selected);
        Assert.AreEqual(5, response.QuestInfos.Length);
        Assert.AreEqual(5, selected.Count);
        Assert.AreEqual(100005, next);
        for (int index = 0; index < selected.Count; index++)
        {
            var placement = catalog.GetByIdentity(selected[index].IdentityType, selected[index].IdentityInstance);
            Assert.IsTrue(candidates.Any(value => value.Identity.Equals(selected[index])));
            var action = response.QuestInfos[index].QuestActions.Single();
            Assert.AreEqual(35, response.QuestInfos[index].Quality);
            Assert.AreEqual(placement.PlayfieldId, action.Playfield.Instance);
            Assert.AreEqual(placement.LocalXBits, BitConverter.SingleToUInt32Bits(action.X));
            Assert.AreEqual(placement.LocalYBits, BitConverter.SingleToUInt32Bits(action.Y));
            Assert.AreEqual(placement.LocalZBits, BitConverter.SingleToUInt32Bits(action.Z));
        }
    }

    [TestMethod]
    public void DestinationRepeatsArePermittedWithinOneDeterministicCohort()
    {
        const int seed = 9;
        int next = 100000;
        var response = GeneratedMissionRollService.Generate(Request(), Owner, 2, 655, 0, 0, MissionLocationSide.Omni,
            1201445827, seed, 4567, () => next++, catalog, 1, 15, out var selected);
        Assert.AreEqual(5, selected.Count);
        Assert.AreEqual(5, response.QuestInfos.Select(value => value.QuestIdentity.Instance).Distinct().Count());
        Assert.IsTrue(selected.Distinct().Count() < selected.Count);
        TestContext.WriteLine("DETERMINISTIC_DESTINATION_REPEAT_SEED=" + seed);
    }

    [TestMethod]
    [DataRow("MissionDestinations.json")]
    [DataRow("MissionEntrancePlacements.json")]
    public void EitherMissingRuntimeFileFailsClosed(string missing)
    {
        using var files = new TemporaryCatalog();
        File.Delete(Path.Combine(files.Directory, missing));
        Assert.ThrowsExactly<FileNotFoundException>(() => MissionDestinationCatalog.Load(files.Root));
    }

    [TestMethod]
    [DataRow("breed")]
    [DataRow("profession")]
    [DataRow("faction")]
    [DataRow("terminal-playfield")]
    [DataRow("terminal-instance")]
    [DataRow("slider")]
    [DataRow("difficulty")]
    public void ObservationMetadataDoesNotInventDestinationRestrictions(string changed)
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
            case "terminal-instance": request.MissionTerminalIdentity = new() { Type = (IdentityType)56001, Instance = unchecked((int)(TerminalInstance + 1)) }; break;
            case "slider": request.GoodBadSlider = 0; break;
            case "difficulty": request.LevelSlider = 2; break;
        }
        int allocations = 0;
        var response = GeneratedMissionRollService.Generate(request, Owner, level,
            terminalPlayfield, 0, 0, side, 1201445827, 17, 4567, () => 100000 + allocations++, catalog, breed, profession, out var selected);
        Assert.AreEqual(5, allocations);
        Assert.AreEqual(5, response.QuestInfos.Length);
        Assert.AreEqual(5, selected.Count);
    }

    [TestMethod]
    public void EveryObservedQualityGeneratesFiveOffersWithDifferentObservationMetadata()
    {
        foreach (int quality in catalog.ExpectedMissionQls)
        {
            var request = CenteredBorealisRequest();
            request.MissionTerminalIdentity = new() { Type = (IdentityType)0xDAC1, Instance = 12345 };
            int next = 100000;
            var response = GeneratedMissionRollService.Generate(request, Owner, quality, 800, 0, 0,
                MissionLocationSide.Clan, 1201445827, 17, 4567, () => next++, catalog, 4, 15, out var selected);
            Assert.AreEqual(5, response.QuestInfos.Length, "QL=" + quality);
            Assert.AreEqual(5, selected.Count);
            Assert.IsTrue(catalog.TryGetDestinations(800, quality, out var eligible, out _));
            Assert.IsTrue(selected.All(identity => eligible.Any(value => value.Identity.Equals(identity))));
            Assert.IsTrue(response.QuestInfos.All(value => value.Quality == quality));
        }
    }

    [TestMethod]
    [DataRow(34)]
    [DataRow(36)]
    [DataRow(200)]
    public void UnsupportedQualityFailsBeforeAllocatingAnyOfferIdentity(int level)
    {
        int allocations = 0;
        Assert.ThrowsExactly<NotSupportedException>(() => GeneratedMissionRollService.Generate(CenteredBorealisRequest(), Owner, level,
            800, 0, 0, MissionLocationSide.Omni, 1201445827, 17, 4567, () => 100000 + allocations++, catalog, 3, 15, out _));
        Assert.AreEqual(0, allocations);
    }

    [TestMethod]
    public void OriginQualityPoolsRemainDistinctAndFallbackUsesOnlyTheExactRequestedQuality()
    {
        Assert.IsTrue(catalog.TryGetDestinations(655, 29, out var andromeda, out bool andromedaFallback));
        Assert.IsTrue(catalog.TryGetDestinations(800, 29, out var borealis, out bool borealisFallback));
        Assert.IsFalse(andromedaFallback);
        Assert.IsFalse(borealisFallback);
        Assert.AreEqual(215, andromeda.Count);
        Assert.AreEqual(196, borealis.Count);
        Assert.AreEqual(0, andromeda.Select(value => value.Identity).Intersect(borealis.Select(value => value.Identity)).Count());
        Assert.IsTrue(catalog.TryGetDestinations(540, 29, out var unknownOrigin, out bool unknownFallback));
        Assert.IsTrue(unknownFallback);
        Assert.AreEqual(411, unknownOrigin.Count);
        Assert.IsTrue(catalog.TryGetDestinations(800, 25, out var ql25, out bool ql25Fallback));
        Assert.IsTrue(ql25Fallback);
        Assert.AreEqual(124, ql25.Count);
        foreach (int origin in new[] { 655, 800, 540 })
        {
            Assert.IsFalse(catalog.TryGetDestinations(origin, 34, out var unsupported, out bool fallback));
            Assert.AreEqual(0, unsupported.Count);
            Assert.IsFalse(fallback);
        }
    }

    [TestMethod]
    [DataRow(655)]
    [DataRow(800)]
    public void GeneratedQuality29CohortsUseTheirOwnOriginPool(int origin)
    {
        var request = CenteredBorealisRequest();
        Assert.IsTrue(catalog.TryGetDestinations(origin, 29, out var candidates, out bool fallback));
        Assert.IsFalse(fallback);
        var expected = candidates.Select(value => value.Identity).ToHashSet();
        int next = 100000;
        var response = GeneratedMissionRollService.Generate(request, Owner, 29, origin, 0, 0,
            MissionLocationSide.Neutral, 1201445827, 17, 4567, () => next++, catalog, 2, 15, out var selected);
        Assert.AreEqual(5, response.QuestInfos.Length);
        Assert.IsTrue(selected.All(expected.Contains));
        Assert.IsTrue(response.QuestInfos.All(value => value.Quality == 29));
    }

    [TestMethod]
    [DataRow("placement-schema")]
    [DataRow("destination-schema")]
    [DataRow("placement-kind")]
    [DataRow("destination-kind")]
    [DataRow("empty-placements")]
    [DataRow("empty-pools")]
    [DataRow("empty-pool")]
    [DataRow("duplicate-placement")]
    [DataRow("duplicate-pool")]
    [DataRow("duplicate-pool-identity")]
    [DataRow("missing-placement")]
    [DataRow("missing-worldpos")]
    [DataRow("position-bits")]
    [DataRow("raw-name")]
    public void CatalogRejectsMalformedOrUnresolvableRuntimeData(string defect)
    {
        using var files = new TemporaryCatalog();
        JsonNode placements = files.Read("MissionEntrancePlacements.json");
        JsonNode destinations = files.Read("MissionDestinations.json");
        JsonArray rows = placements["Placements"]!.AsArray();
        JsonArray pools = destinations["Pools"]!.AsArray();
        JsonArray identities = pools[0]!["DestinationIdentities"]!.AsArray();
        switch (defect)
        {
            case "placement-schema": placements["SchemaVersion"] = 1; break;
            case "destination-schema": destinations["SchemaVersion"] = 2; break;
            case "placement-kind": placements["CatalogKind"] = "OTHER"; break;
            case "destination-kind": destinations["CatalogKind"] = "OTHER"; break;
            case "empty-placements": rows.Clear(); break;
            case "empty-pools": pools.Clear(); break;
            case "empty-pool": identities.Clear(); break;
            case "duplicate-placement": rows.Add(rows[0]!.DeepClone()); break;
            case "duplicate-pool": pools.Add(pools[0]!.DeepClone()); break;
            case "duplicate-pool-identity": identities.Add(identities[0]!.DeepClone()); break;
            case "missing-placement": identities[0]!["IdentityInstance"] = 0xD1234567u; break;
            case "missing-worldpos": rows.Single(row => ReadIdentity(row).Equals(ReadIdentity(identities[0])))!["WorldPos"] = null; break;
            case "position-bits": rows[0]!["LocalXBits"] = rows[0]!["LocalXBits"]!.GetValue<uint>() ^ 1u; break;
            case "raw-name": rows[0]!["RawNameHex"] = "00"; break;
        }
        files.Write("MissionEntrancePlacements.json", placements);
        files.Write("MissionDestinations.json", destinations);
        Assert.ThrowsExactly<InvalidDataException>(() => MissionDestinationCatalog.Load(files.Root), defect);
    }

    [TestMethod]
    public void DataOnlyPlacementAndPoolExpansionNeedsNoCountHashOrRuntimeCodeChange()
    {
        using var files = new TemporaryCatalog();
        const uint addedInstance = 0xD1234567u;
        const int addedOrigin = 999;
        const int addedQuality = 34;
        Assert.IsFalse(catalog.TryGetByIdentity(0xDAC6, addedInstance, out _));
        Assert.IsFalse(catalog.TryGetObservedDestinations(addedQuality, out _));
        JsonNode placements = files.Read("MissionEntrancePlacements.json");
        JsonArray rows = placements["Placements"]!.AsArray();
        JsonNode added = rows.First(row => row!["WorldPos"] != null)!.DeepClone();
        added["IdentityInstance"] = addedInstance;
        float x = MathF.BitIncrement((float)added["LocalX"]!.GetValue<double>());
        added["LocalX"] = (double)x;
        added["LocalXBits"] = BitConverter.SingleToUInt32Bits(x);
        rows.Add(added);
        JsonNode destinations = files.Read("MissionDestinations.json");
        destinations["Pools"]!.AsArray().Add(new JsonObject
        {
            ["TerminalPlayfieldId"] = addedOrigin,
            ["ExpectedMissionQl"] = addedQuality,
            ["DestinationIdentities"] = new JsonArray(new JsonObject
            {
                ["IdentityType"] = 0xDAC6u, ["IdentityInstance"] = addedInstance
            })
        });
        files.Write("MissionEntrancePlacements.json", placements);
        files.Write("MissionDestinations.json", destinations);
        var expanded = MissionDestinationCatalog.Load(files.Root);
        Assert.AreEqual(catalog.Count + 1, expanded.Count);
        Assert.AreEqual(catalog.ObservedDestinations.Count + 1, expanded.ObservedDestinations.Count);
        Assert.AreEqual(catalog.TerminalPoolCount + 1, expanded.TerminalPoolCount);
        Assert.IsTrue(expanded.TryGetDestinations(addedOrigin, addedQuality, out var pool, out bool fallback));
        Assert.IsFalse(fallback);
        Assert.AreEqual(addedInstance, pool.Single().IdentityInstance);
        int next = 100000;
        var response = GeneratedMissionRollService.Generate(CenteredBorealisRequest(), Owner, addedQuality, addedOrigin, 0, 0,
            MissionLocationSide.Neutral, 1201445827, 17, 4567, () => next++, expanded, 2, 15, out var selected);
        Assert.AreEqual(5, response.QuestInfos.Length);
        Assert.AreEqual(5, selected.Count);
        Assert.IsTrue(selected.All(identity => identity.Equals(pool.Single().Identity)));
        Assert.AreEqual(5, response.QuestInfos.Select(value => value.QuestIdentity.Instance).Distinct().Count());
        Assert.IsTrue(response.QuestInfos.All(value => BitConverter.SingleToUInt32Bits(value.QuestActions.Single().X) == BitConverter.SingleToUInt32Bits(x)));
    }

    [TestMethod]
    public void ShadeLevel25NeutralBorealisUsesQualityFallbackAndPublishesFiveOffersThenChargesOnce()
    {
        // Live acceptance retained level 25 / neutral / breed 2 / profession 15 in PF 800,
        // terminal 0xDAC1:0xC0020320, detent 6 and six raw zero secondary sliders.
        using var fixture = new RollFixture(25);
        fixture.Dao.BeforePublish = batch =>
        {
            Assert.IsTrue(Monitor.IsEntered(fixture.Player.PersistenceGate));
            Assert.AreEqual(5000, fixture.Player.Stats.Get(CharacterStat.Cash, StatDetail.Base));
            Assert.AreEqual(5000, batch.CurrentCash);
            Assert.AreEqual(5, batch.Offers.Count);
            Assert.AreEqual(0, fixture.Session.Sent.OfType<QuestAlternativeMessage>().Count());
            Assert.IsTrue(catalog.TryGetDestinations(800, 25, out var candidates, out bool fallback));
            Assert.IsTrue(fallback);
            foreach (var offer in batch.Offers)
            {
                var identity = new MissionPlacementIdentity(unchecked((uint)offer.EntranceType), unchecked((uint)offer.EntranceInstance));
                Assert.IsTrue(candidates.Any(value => value.Identity.Equals(identity)));
                var placement = catalog.GetByIdentity(identity.IdentityType, identity.IdentityInstance);
                Assert.IsTrue(catalog.TryGetWorldPosition(identity, out var position));
                Assert.AreEqual(placement.PlayfieldId, offer.DestinationPlayfield);
                Assert.AreEqual(placement.LocalXBits, BitConverter.SingleToUInt32Bits(offer.DestinationX));
                Assert.AreEqual(placement.LocalYBits, BitConverter.SingleToUInt32Bits(offer.DestinationY));
                Assert.AreEqual(placement.LocalZBits, BitConverter.SingleToUInt32Bits(offer.DestinationZ));
                Assert.AreEqual(position.WorldOffsetX, offer.EntranceLow);
                Assert.AreEqual(position.WorldOffsetZ, offer.EntranceHigh);
            }
        };
        fixture.Handle();
        Assert.AreEqual(1, fixture.Dao.PublishCalls);
        Assert.IsNotNull(fixture.Dao.Batch);
        Assert.AreEqual(5000 - fixture.Dao.Batch.Fee, fixture.Player.Stats.Get(CharacterStat.Cash, StatDetail.Base));
        Assert.AreEqual(5, fixture.Session.Sent.OfType<QuestAlternativeMessage>().Single().QuestInfos.Length);
        Assert.IsFalse(fixture.Player.IsPersistenceQuarantined);
    }

    [TestMethod]
    public void UnsupportedQualityHandlerDoesNotPublishChargeOrSendOffers()
    {
        using var fixture = new RollFixture(34);
        fixture.Handle();
        Assert.AreEqual(0, fixture.Dao.PublishCalls);
        Assert.IsNull(fixture.Dao.Batch);
        Assert.AreEqual(5000, fixture.Player.Stats.Get(CharacterStat.Cash, StatDetail.Base));
        Assert.AreEqual(0, fixture.Session.Sent.OfType<QuestAlternativeMessage>().Count());
        Assert.IsTrue(fixture.Session.Sent.OfType<ChatTextMessage>().Any(value => value.Text.Contains("No credits were deducted", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedPublicationNeverChargesOrSendsGeneratedOffers(bool throws)
    {
        using var fixture = new RollFixture(25);
        fixture.Dao.RejectPublication = true;
        fixture.Dao.ThrowPublication = throws;
        fixture.Handle();
        Assert.AreEqual(1, fixture.Dao.PublishCalls);
        Assert.IsNotNull(fixture.Dao.Batch);
        Assert.AreEqual(5, fixture.Dao.Batch.Offers.Count);
        Assert.AreEqual(5000, fixture.Player.Stats.Get(CharacterStat.Cash, StatDetail.Base));
        Assert.AreEqual(0, fixture.Session.Sent.OfType<QuestAlternativeMessage>().Count());
        Assert.IsFalse(fixture.Player.IsPersistenceQuarantined);
    }

    static QuestAlternativeMessage CenteredBorealisRequest() => new()
    {
        Identity = Owner, VersionId = 4, LevelSlider = 6,
        MissionTerminalIdentity = new() { Type = (IdentityType)0xDAC1, Instance = unchecked((int)0xC0000320u) },
        GoodBadSlider = 255, OrderChaosSlider = 255, OpenHiddenSlider = 255,
        PhysicalMysticalSlider = 255, HeadOnStealthSlider = 255, MoneyExperienceSlider = 255,
        QuestInfos = []
    };

    static QuestAlternativeMessage LiveShadeRequest()
    {
        var request = CenteredBorealisRequest();
        request.MissionTerminalIdentity = new() { Type = (IdentityType)0xDAC1, Instance = unchecked((int)0xC0020320u) };
        request.GoodBadSlider = request.OrderChaosSlider = request.OpenHiddenSlider = 0;
        request.PhysicalMysticalSlider = request.HeadOnStealthSlider = request.MoneyExperienceSlider = 0;
        return request;
    }

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

    static MissionPlacementIdentity ReadIdentity(JsonNode? node) => new(
        node!["IdentityType"]!.GetValue<uint>(), node["IdentityInstance"]!.GetValue<uint>());

    sealed class TemporaryCatalog : IDisposable
    {
        static readonly string[] Files = ["MissionEntrancePlacements.json", "MissionDestinations.json"];
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "aorebirth-mission-destination-" + Guid.NewGuid().ToString("N"));
        internal string Directory => Path.Combine(Root, "Missions", "Destinations");
        internal TemporaryCatalog()
        {
            System.IO.Directory.CreateDirectory(Directory);
            string source = Path.Combine(FindRepositoryRoot(), "AORebirth", "GameData", "Missions", "Destinations");
            foreach (string name in Files) File.Copy(Path.Combine(source, name), Path.Combine(Directory, name));
        }
        internal JsonNode Read(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(Directory, name)))!;
        internal void Write(string name, JsonNode data) => File.WriteAllText(Path.Combine(Directory, name), data.ToJsonString());
        public void Dispose()
        {
            foreach (string name in Files) File.Delete(Path.Combine(Directory, name));
            System.IO.Directory.Delete(Directory);
            System.IO.Directory.Delete(Path.Combine(Root, "Missions"));
            System.IO.Directory.Delete(Root);
        }
    }

    sealed class RollFixture : IDisposable
    {
        internal readonly Player Player = TestWorld.CreatePlayer(Owner.Instance);
        internal readonly RollSession Session = new();
        internal readonly RollDao Dao = new();
        readonly ServiceProvider _services;
        readonly QuestAlternativeMessageHandler _handler;

        internal RollFixture(int level)
        {
            // The existing test convention avoids loading unrelated NPCs or starting a heartbeat.
            var playfield = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
            typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(playfield, new Identity { Type = IdentityType.Playfield, Instance = 800 });
            var registry = new DynelRegistry();
            var terminal = new MissionTerminal(LiveShadeRequest().MissionTerminalIdentity, new ItemTemplate());
            terminal.Playfield = playfield;
            registry.Register(terminal);
            _services = new ServiceCollection().AddSingleton(registry).AddSingleton(new PlayfieldLocality(800, null)).BuildServiceProvider();
            typeof(Playfield).GetField("_serviceProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(playfield, _services);
            Player.Playfield = playfield;
            Player.Position = terminal.Position;
            Player.Stats.Set(CharacterStat.Level, level);
            Player.Stats.Set(CharacterStat.Side, 0);
            Player.Stats.Set(CharacterStat.Breed, 2);
            Player.Stats.Set(CharacterStat.Profession, 15);
            Player.Stats.Set(CharacterStat.Cash, 5000, StatDetail.Base);
            Player.Session = Session;
            Session.BindPlayer(Player);
            _handler = new QuestAlternativeMessageHandler(Dao, new GeneratedMissionService(Dao, new StubLogger()), catalog);
        }
        internal void Handle() => _handler.Handle(LiveShadeRequest(), Session);
        public void Dispose() => _services.Dispose();
    }

    sealed class RollSession : IZoneSession, IGameTimeSession
    {
        public SessionState State { get; set; } = SessionState.InPlay;
        public Player? Player { get; private set; }
        internal readonly List<MessageBody> Sent = [];
        public DateTime? GameTimeSynchronizedAtUtc { get; private set; } = DateTime.UtcNow;
        public int GameTimeServerSeconds { get; private set; } = 1201445827;
        public void RecordGameTimeSynchronization(DateTime utcNow, int serverSeconds)
        { GameTimeSynchronizedAtUtc = utcNow; GameTimeServerSeconds = serverSeconds; }
        public void BindPlayer(Player player) => Player = player;
        public void UnbindPlayer() => Player = null;
        public void TransferToPlayfield(Playfield destination, AORebirth.Core.Vector.Vector3 landing) => throw new AssertFailedException();
        public void Send(byte[] packet) => throw new AssertFailedException("Unexpected raw packet.");
        public void Send(Message message) => Sent.Add(message.Body);
        public void Send(MessageBody body) => Sent.Add(body);
        public void Send(MessageBody body, int sender, int receiver) => Sent.Add(body);
        public void SendInitiateCompression() => throw new AssertFailedException();
        public void Close() => State = SessionState.Closed;
    }

    sealed class RollDao : IGeneratedMissionDao
    {
        internal int PublishCalls;
        internal GeneratedMissionOfferBatch? Batch;
        internal Action<GeneratedMissionOfferBatch>? BeforePublish;
        internal bool RejectPublication, ThrowPublication;
        public int ReserveIdentities(string sequence, int count)
        { Assert.AreEqual("offer", sequence); Assert.AreEqual(5, count); return 100000; }
        public GeneratedMissionResult PublishOffers(GeneratedMissionOfferBatch batch)
        {
            PublishCalls++;
            Batch = batch;
            BeforePublish?.Invoke(batch);
            if (ThrowPublication) throw new InvalidOperationException("Injected known publication rollback.");
            return new() { Status = RejectPublication ? GeneratedMissionResultStatus.Rejected : GeneratedMissionResultStatus.Applied,
                Cash = RejectPublication ? batch.CurrentCash : batch.CurrentCash - batch.Fee };
        }
        public IList<GeneratedMissionOffer> ReadOffers(int owner) => throw new AssertFailedException();
        public GeneratedMissionResult Accept(GeneratedMissionAcceptance acceptance) => throw new AssertFailedException();
        public IList<GeneratedMissionBinding> ReadAccepted(int owner) => throw new AssertFailedException();
        public GeneratedMissionBinding ReadAccepted(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult Observe(GeneratedMissionObservation observation) => throw new AssertFailedException();
        public GeneratedMissionResult Complete(GeneratedMissionCompletion completion) => throw new AssertFailedException();
        public GeneratedMissionResult End(int owner, int type, int instance, GeneratedMissionState state, long now) => throw new AssertFailedException();
        public GeneratedMissionResult AdvanceCleanup(int owner, int type, int instance, long version, long mask, long now) => throw new AssertFailedException();
        public IList<GeneratedMissionObject> ReadObjects(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult UpdateObjects(int owner, int type, int instance, IList<GeneratedMissionObject> objects, long now) => throw new AssertFailedException();
        public GeneratedMissionResult SavePosition(int owner, int type, int instance, int pf, float x, float y, float z, long now) => throw new AssertFailedException();
        public IList<MissionItemInstanceData> ReadArtifacts(int owner, int type, int instance) => throw new AssertFailedException();
        public GeneratedMissionResult CleanupArtifacts(int owner, int type, int instance, long now) => throw new AssertFailedException();
        public GeneratedMissionResult ClaimCorpseCredits(int owner, int type, int instance, int npc, int cash, long now) => throw new AssertFailedException();
    }
}
