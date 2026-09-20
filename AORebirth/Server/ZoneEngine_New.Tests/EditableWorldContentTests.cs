namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class EditableWorldContentTests
{
    [TestMethod]
    public void ScarlettPlacementRotationAppearanceAndStatsChangeWithTheSameBinary()
    {
        using var fixture = new TempContent();
        string original = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GameData", "Playfields", "7010", "Npcs.json"));
        string playfieldPath = Path.Combine(fixture.Root, "Playfields", "7010");
        Directory.CreateDirectory(playfieldPath);
        File.WriteAllText(Path.Combine(playfieldPath, "Npcs.json"), original);
        Guid binary = typeof(WorldNpcFactory).Assembly.ManifestModule.ModuleVersionId;
        var a = WorldNpcFactory.Create(PlayfieldNpcContentCatalog.Load(fixture.Root, 7010).Npcs.Single(n => n.Name == "Scarlett Dalquist"), new StubItemBuilder());
        var document = JsonNode.Parse(original)!;
        var row = document["Npcs"]!.AsArray().Single(n => n!["Name"]!.GetValue<string>() == "Scarlett Dalquist")!;
        row["Position"] = new JsonArray(31f, 7f, 92f);
        row["Rotation"] = new JsonArray(0f, 1f, 0f, 0f);
        row["Stats"]![((int)CharacterStat.Scale).ToString()] = 131;
        row["Textures"]![0]!["Id"] = 1234;
        row["Meshes"]![0]!["Id"] = 5678;
        File.WriteAllText(Path.Combine(playfieldPath, "Npcs.json"), document.ToJsonString());
        var b = WorldNpcFactory.Create(PlayfieldNpcContentCatalog.Load(fixture.Root, 7010).Npcs.Single(n => n.Name == "Scarlett Dalquist"), new StubItemBuilder());
        Assert.AreEqual(binary, typeof(WorldNpcFactory).Assembly.ManifestModule.ModuleVersionId);
        Assert.AreEqual(104.180695f, a.Position.xf); Assert.AreEqual(31f, b.Position.xf);
        Assert.AreEqual(-0.342263281f, a.Rotation.yf); Assert.AreEqual(1f, b.Rotation.yf);
        Assert.AreEqual(117, a.Stats.GetOrZero(CharacterStat.Scale)); Assert.AreEqual(131, b.Stats.GetOrZero(CharacterStat.Scale));
        Assert.AreEqual(213851, a.Textures[0].Texture); Assert.AreEqual(1234, b.Textures[0].Texture);
        Assert.AreEqual(223846u, a.Meshes[0].Id); Assert.AreEqual(5678u, b.Meshes[0].Id);
    }

    [TestMethod]
    [DataRow(4582)]
    [DataRow(4544)]
    [DataRow(800)]
    public void EditablePlacementAndResolvedTemplateRegisterWithoutEvidenceRow(int playfieldId)
    {
        using var fixture = new TempContent();
        File.WriteAllText(Path.Combine(fixture.Root, "NpcTemplates.json"), "{\"TEST\":{\"Templates\":[{\"Name\":\"Editable fixture\",\"Level\":10,\"Stats\":{\"54\":10,\"1\":100}}]}}");
        var path = Path.Combine(fixture.Root, "Playfields", playfieldId.ToString()); Directory.CreateDirectory(path);
        var entry = new PlayfieldSpawnEntry { HashText = "TEST", Position = [4, 5, 6], Radius = 0, MinLevel = 10, MaxLevel = 10, RespawnChance = 100, RespawnTime = 30 };
        File.WriteAllText(Path.Combine(path, "Spawns.json"), JsonSerializer.Serialize(new PlayfieldSpawnsData { SchemaVersion = 1, PlayfieldId = playfieldId, Spawns = [entry] }));
        var data = new GameDataStore(new StubLogger(), null, fixture.Root);
        Assert.AreEqual(1, Register(playfieldId, data));
        entry.Position = [99, 12, 17]; entry.Angle = 41; entry.RespawnTime = 47;
        File.WriteAllText(Path.Combine(path, "Spawns.json"), JsonSerializer.Serialize(new PlayfieldSpawnsData { SchemaVersion = 1, PlayfieldId = playfieldId, Spawns = [entry] }));
        Assert.AreEqual(1, Register(playfieldId, new GameDataStore(new StubLogger(), null, fixture.Root)));
        entry.HashText = "UNRESOLVED";
        File.WriteAllText(Path.Combine(path, "Spawns.json"), JsonSerializer.Serialize(new PlayfieldSpawnsData { SchemaVersion = 1, PlayfieldId = playfieldId, Spawns = [entry] }));
        Assert.AreEqual(0, Register(playfieldId, new GameDataStore(new StubLogger(), null, fixture.Root)));
    }

    [TestMethod]
    public void MissingHashAndAaaaDoNotCreateGameplaySpawnPoints()
    {
        var catalog = NpcTemplateCatalog.Parse("{\"AAAA\":{\"Templates\":[{\"Name\":\"Marker\",\"Level\":1,\"Stats\":{}}]}}");
        Assert.IsTrue(catalog.CanResolve("AAAA"));
        Assert.IsTrue(catalog.TryResolve("AAAA", 1, out MobTemplate aaaa));
        Assert.AreEqual("Marker", aaaa.Name);
        Assert.IsFalse(aaaa.Attackable);
        Assert.IsTrue(aaaa.UnresolvedPlaceholder);
        Assert.IsFalse(NpcTemplateValidation.CanSpawn(aaaa));
        Assert.IsFalse(catalog.CanResolve("MISSING"));
        Assert.IsFalse(catalog.TryResolve("MISSING", 1, out _));

        using var fixture = new TempContent();
        File.WriteAllText(Path.Combine(fixture.Root, "NpcTemplates.json"), "{\"AAAA\":{\"Templates\":[{\"Name\":\"Marker\",\"Level\":1,\"Stats\":{\"54\":1}}]}}");
        var path = Path.Combine(fixture.Root, "Playfields", "4582"); Directory.CreateDirectory(path);
        var entry = new PlayfieldSpawnEntry { HashText = "UNRESOLVED", Position = [4, 5, 6], Radius = 0, MinLevel = 1, MaxLevel = 1, RespawnChance = 100, RespawnTime = 30 };
        File.WriteAllText(Path.Combine(path, "Spawns.json"), JsonSerializer.Serialize(new PlayfieldSpawnsData { SchemaVersion = 1, PlayfieldId = 4582, Spawns = [entry] }));
        Assert.AreEqual(0, Register(4582, new GameDataStore(new StubLogger(), null, fixture.Root)));
        entry.HashText = MobTemplate.FallbackHash;
        File.WriteAllText(Path.Combine(path, "Spawns.json"), JsonSerializer.Serialize(new PlayfieldSpawnsData { SchemaVersion = 1, PlayfieldId = 4582, Spawns = [entry] }));
        Assert.AreEqual(0, Register(4582, new GameDataStore(new StubLogger(), null, fixture.Root)));
    }

    [TestMethod]
    public void InvalidPlacementIsRejectedByStructureWithoutReviewMetadata()
    {
        var entry = new PlayfieldSpawnEntry { HashText = "TEST", Position = [1, 2, 3], MinLevel = 1, MaxLevel = 2, RespawnChance = 100 };
        Assert.IsTrue(SpawnContentValidation.IsValid(entry));
        entry.Position[0] = float.NaN; Assert.IsFalse(SpawnContentValidation.IsValid(entry));
        entry.Position[0] = 1; entry.MaxLevel = 0; Assert.IsFalse(SpawnContentValidation.IsValid(entry));
        entry.MaxLevel = 2; entry.Radius = -1; Assert.IsFalse(SpawnContentValidation.IsValid(entry));
    }

    static int Register(int id, IGameData data)
    {
        var field = (Playfield)RuntimeHelpers.GetUninitializedObject(typeof(Playfield));
        typeof(Playfield).GetField("<Identity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(field, new Identity { Type = IdentityType.Playfield2, Instance = id });
        var service = (SpawnService)RuntimeHelpers.GetUninitializedObject(typeof(SpawnService));
        var system = new HashSpawnSystem(field, service, data, new StubLogger());
        system.Initialize(new PlayfieldLocality(id, null));
        return system.PointCount;
    }
    sealed class TempContent : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "aorebirth-world-content-test-" + Guid.NewGuid().ToString("N"));
        internal TempContent() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
