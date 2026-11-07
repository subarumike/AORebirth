namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Missions;

[TestClass]
public sealed class MissionOfferCatalogTests
{
    // Public RollBodies.json from current-master intake. The byte payloads are
    // already retained unchanged in the linked historical capture fixture.
    static readonly MissionOfferSource Source = new(
        "2b21b1e2896250d15a8eb736a5452164a071539a3a56cfbdd8779a422d8b77aa",
        "cda38284540f45573424fb399a60c9ab7627a6f7");

    [TestMethod]
    public void EveryCapturedRollBodyProjectsAndSerializesByteForByte()
    {
        byte[][] bodies = MissionRollCaptureLibrary.CapturedRollBodiesHex.Select(Convert.FromHexString).ToArray();
        var content = new MissionOfferContent(1, Source, bodies.Select(body => Project(body, GeneratedMissionWire.Read(body))).ToImmutableArray());
        var exportOptions = new JsonSerializerOptions(MissionOfferCatalog.JsonOptions) { NewLine = "\n" };
        byte[] generated = JsonSerializer.SerializeToUtf8Bytes(content, exportOptions);
        var roundTrip = JsonSerializer.Deserialize<MissionOfferContent>(generated, MissionOfferCatalog.JsonOptions)!;
        for (int index = 0; index < bodies.Length; index++)
        {
            var definition = roundTrip.Responses[index];
            var response = definition.Create(definition.SourceCharacter.Create(), definition.SourceTerminal.Create());
            response.QuestInfos = definition.Offers.Select(offer => offer.Create()).ToArray();
            CollectionAssert.AreEqual(bodies[index], GeneratedMissionWire.Write(response), $"Historical response {index}");
        }

        string? output = Environment.GetEnvironmentVariable("AO_REBIRTH_MISSION_OFFER_EXPORT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
            file.Write(generated);
            return;
        }

        string catalogPath = Path.Combine(MissionContentJson.RootPath, MissionOfferCatalog.FileName);
        CollectionAssert.AreEqual(generated, File.ReadAllBytes(catalogPath), "The runtime artifact must match the verified typed projection.");
        var catalog = MissionOfferCatalog.Load(MissionContentJson.RootPath);
        Assert.AreEqual(bodies.Length, catalog.ResponseCount);
        Assert.AreEqual(bodies.Length * MissionOfferCatalog.OfferCount, catalog.DefinitionCount);
    }

    [TestMethod]
    public void MissingNestedIdentityFieldIsRejected()
        => Rejects<JsonException>(catalog =>
        {
            var character = catalog["Responses"]![0]!["SourceCharacter"]!.AsObject();
            Assert.IsTrue(character.Remove("Instance"));
        });

    [TestMethod]
    public void IncompleteOfferCohortIsRejected()
        => Rejects<InvalidDataException>(catalog => catalog["Responses"]![0]!["Offers"]!.AsArray().RemoveAt(0));

    [TestMethod]
    public void UndeclaredPacketPayloadFieldIsRejected()
        => Rejects<JsonException>(catalog => catalog["Responses"]![0]!["RawBody"] = "00");

    static void Rejects<TException>(Action<JsonObject> mutate) where TException : Exception
    {
        string source = Path.Combine(MissionContentJson.RootPath, MissionOfferCatalog.FileName);
        var catalog = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        mutate(catalog);
        var directory = Directory.CreateTempSubdirectory();
        string path = Path.Combine(directory.FullName, MissionOfferCatalog.FileName);
        try
        {
            File.WriteAllText(path, catalog.ToJsonString());
            Assert.ThrowsExactly<TException>(() => MissionOfferCatalog.Load(directory.FullName));
        }
        finally
        {
            File.Delete(path);
            directory.Delete();
        }
    }

    static MissionResponseDefinition Project(byte[] body, QuestAlternativeMessage response)
        => new(Convert.ToHexStringLower(SHA256.HashData(body)), Wire(response.Identity), Wire(response.MissionTerminalIdentity),
            response.Unknown, response.VersionId, response.LevelSlider, response.GoodBadSlider, response.OrderChaosSlider,
            response.OpenHiddenSlider, response.PhysicalMysticalSlider, response.HeadOnStealthSlider,
            response.MoneyExperienceSlider, response.Unknown4, response.Unknown5, response.QuestInfos.Select(Project).ToImmutableArray());

    static MissionOfferDefinition Project(QuestInfo offer)
    {
        QuestItemShort reward = offer.ItemRewards.Single();
        QuestActionList action = offer.QuestActions.Single();
        return new MissionOfferDefinition(Wire(offer.QuestIdentity), offer.Unknown1, offer.ShortInfo, offer.Info,
            Wire(offer.Unknown5), offer.RewardDescriptorVersion, offer.CashReward, offer.ExperienceReward,
            new(reward.LowId, reward.HighId, reward.Quality, reward.Unknown1), offer.UnknownHash, offer.Quality,
            Wire(offer.Unknown14), offer.MissionIconId, offer.Unknown16,
            new(action.Version, Wire(action.Action), Wire(action.Unknown1), Wire(action.Unknown2), action.UnknownHash15,
                Wire(action.Playfield), action.Unknown18, action.Unknown19, action.X, action.Y, action.Z),
            offer.Unknown20, offer.Unknown22, Wire(offer.Unknown23), offer.Unknown26, offer.Unknown27);
    }

    static MissionWireIdentity Wire(Identity value) => new(value.Type, value.Instance);
}
