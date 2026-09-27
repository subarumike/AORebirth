namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AORebirth.Interfaces.Persistence.Missions;
using ZoneEngine.Core.Missions;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;

[TestClass]
public sealed class MissionPacketIndependenceTests
{
    [TestMethod]
    public void ProductionSourcesHaveNoHistoricalMissionPacketConsumers()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AI_START_HERE.md"))) root = root.Parent;
        Assert.IsNotNull(root);
        string production = Path.Combine(root.FullName, "AORebirth", "Server", "ZoneEngine_New");
        string[] forbidden = ["RollBodies.json", "MissionRollCaptureLibrary", "MissionRollCaptureTemplate", "RawPacketHex",
            "PacketHex", "CapturedBody(", "RetargetOpaqueCapturedPacket", "CopySpawnMessage(", "ValidateCaptureCounts("];
        foreach (string file in Directory.EnumerateFiles(production, "*.cs", SearchOption.AllDirectories))
        foreach (string token in forbidden)
            Assert.IsFalse(File.ReadAllText(file).Contains(token, StringComparison.Ordinal), file + " contains " + token);
        Assert.IsFalse(typeof(GeneratedMissionWire).GetFields(BindingFlags.Static | BindingFlags.NonPublic).Any(field => field.FieldType == typeof(byte[][])));
    }

    [TestMethod]
    public void PermanentMissionInputsContainNoEmbeddedHistoricalPacketPayloads()
    {
        foreach (string name in new[] { "MissionOffers.json", "Layouts.json" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(MissionContentJson.RootPath, name)));
            Check(doc.RootElement);
        }
        static void Check(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
                foreach (var property in value.EnumerateObject())
                {
                    Assert.IsFalse(new[] { "RawPacketHex", "PacketHex", "RawPacket", "PacketBytes", "RawBody", "UndecodedTail", "RetargetSlots" }.Contains(property.Name, StringComparer.OrdinalIgnoreCase));
                    Check(property.Value);
                }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (var item in value.EnumerateArray()) Check(item);
        }
    }

    [TestMethod]
    public void EveryPermanentStaticObjectSerializesWithLiveIdentityAndNoHistoricalDecoder()
    {
        var codec = new ZoneMessageCodec();
        int count = 0;
        foreach (var bundle in MissionAcgCapturedLayoutCatalog.CreateBundles())
        {
            var binding = new GeneratedMissionBinding { LivePlayfield = 2000000001, OwnerId = 12345 };
            var definitions = bundle.Dynels.Select(value => value.Wire.Spawn)
                .Concat(bundle.ObjectiveSlots.Select(value => value.Spawn).Where(value => value != null));
            foreach (var definition in definitions)
            {
                var identity = new MissionAcgIdentityRecord(51017, 10000 + count);
                var body = definition.Create(binding, identity);
                var bytes = codec.Serialize(body, 0, binding.OwnerId);
                var message = codec.Deserialize(bytes)!;
                CollectionAssert.AreEqual(bytes, codec.Serialize(message));
                count++;
            }
        }
        Assert.AreEqual(152, count);
    }

    [TestMethod]
    public void OfferDefinitionsAreCopiedWithoutSharingMutableState()
    {
        var content = MissionOfferContent.Current;
        Assert.AreEqual(13, content.Responses.Length);
        foreach (int index in Enumerable.Range(0, content.Responses.Length))
        {
            var one = content.CopyResponse(index);
            var two = content.CopyResponse(index);
            CollectionAssert.AreEqual(SmokeLounge.AOtomation.Messaging.Tests.MissionHistoricalTestPackets.CapturedBody(index), GeneratedMissionWire.Write(one), "Offline normalization must preserve every measured offer field.");
            CollectionAssert.AreEqual(GeneratedMissionWire.Write(one), GeneratedMissionWire.Write(two));
            one.QuestInfos[0].CashReward++;
            Assert.AreNotEqual(one.QuestInfos[0].CashReward, two.QuestInfos[0].CashReward);
        }
    }
    [TestMethod]
    public void EveryNpcAppearanceAndDurableBundleIdentityMatchesItsOfflineSource()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AI_START_HERE.md"))) root = root.Parent;
        Assert.IsNotNull(root);
        using var historical = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "Tests", "Fixtures", "Gameplay", "Missions", "HistoricalLayouts.json")));
        var bundles = MissionAcgCapturedLayoutCatalog.CreateBundles();
        var codec = new ZoneMessageCodec();
        int checkedAppearances = 0;
        foreach (var source in historical.RootElement.EnumerateArray())
        {
            var bundle = bundles.Single(value => value.LayoutId == source.GetProperty("LayoutId").GetString());
            Assert.AreEqual(source.GetProperty("ExpectedGeneratorPayloadSha256").GetString(), bundle.ExpectedGeneratorPayloadSha256);
            CollectionAssert.AreEqual(Convert.FromHexString(bundle.ExpectedGeneratorPayloadSha256), Convert.FromHexString(bundle.GeneratorPayloadSha256));
            foreach (string collection in new[] { "NpcSlots", "ObjectiveSlots" })
            foreach (var slot in source.GetProperty(collection).EnumerateArray())
            {
                var body = codec.Deserialize(Convert.FromHexString(slot.GetProperty("RawPacketHex").GetString()!))!.Body;
                if (body is not SmokeLounge.AOtomation.Messaging.Messages.N3Messages.SimpleCharFullUpdateMessage npc) continue;
                int number = slot.GetProperty("Slot").GetInt32();
                var appearance = collection == "NpcSlots" ? bundle.NpcSlots.Single(value => value.Slot == number).Appearance
                    : bundle.ObjectiveSlots.Single(value => value.Slot == number).Appearance;
                Assert.IsNotNull(appearance);
                Assert.AreEqual(npc.MonsterScale, appearance.Scale);
                Assert.AreEqual(npc.HeadMesh, appearance.HeadMesh);
                Assert.AreEqual(JsonSerializer.Serialize(npc.Textures ?? [], MissionTypedJson.Options), JsonSerializer.Serialize(appearance.Textures, MissionTypedJson.Options));
                Assert.AreEqual(JsonSerializer.Serialize(npc.Meshes ?? [], MissionTypedJson.Options), JsonSerializer.Serialize(appearance.Meshes, MissionTypedJson.Options));
                checkedAppearances++;
            }
        }
        Assert.AreEqual(81, checkedAppearances);
    }

    [TestMethod]
    public void TypedContentRejectsConflictingPlacementAndOwnedWorldObjects()
    {
        var dynel = MissionAcgCapturedLayoutCatalog.CreateBundles().SelectMany(bundle => bundle.Dynels).First(value => value.Wire.Spawn.Chest != null);
        var content = MissionTypedJson.Copy(dynel.Wire.Spawn);
        content.ValidatePlacement(dynel.CapturedIdentity, dynel.CapturedPlayfield2);
        Assert.ThrowsExactly<InvalidDataException>(() => content.ValidatePlacement(new MissionAcgIdentityRecord(dynel.CapturedIdentity.Type, dynel.CapturedIdentity.Instance + 1), dynel.CapturedPlayfield2));
        content.Chest!.Owner = new SmokeLounge.AOtomation.Messaging.GameData.Identity { Type = SmokeLounge.AOtomation.Messaging.GameData.IdentityType.CanbeAffected, Instance = 1 };
        Assert.ThrowsExactly<InvalidDataException>(() => content.Validate());
    }
}
