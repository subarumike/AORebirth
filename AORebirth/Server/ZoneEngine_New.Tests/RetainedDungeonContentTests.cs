namespace ZoneEngine_New.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Quests.Dungeons;

[TestClass]
public sealed class RetainedDungeonContentTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RestoresOriginalRoomGeometryAcrossRevisionsAndRejectsMissingOrChangedAssets(bool team)
    {
        using var fixture = new RetainedAssets();
        fixture.Publish(doorOpenChance: 0);
        var runtimeA = fixture.CreateGenerator();
        QuestDungeonParameters accepted = fixture.Mission();
        Assert.IsTrue(runtimeA.ConfigureNewDungeon(accepted, team));
        Assert.IsNotNull(accepted.Content);
        Assert.IsTrue(runtimeA.TryGenerate(accepted, out var original));
        Assert.IsNotNull(original.Geometry);
        Assert.IsTrue(original.Geometry.Collision.HasCollision);
        Assert.AreEqual(original.Generator.Rooms.Length, original.Geometry.Placements.Count);
        Assert.AreEqual(team, original.FloorTransitions.Count > 0);
        Assert.AreEqual(0, original.Doors?.OpenChancePercent);
        Assert.IsTrue(QuestDungeonParameters.TryParse(accepted.ToJson(), out var restored));

        fixture.Publish(doorOpenChance: 100);
        var runtimeB = fixture.CreateGenerator();
        Assert.IsTrue(runtimeB.TryGenerate(restored, out var replay));
        CollectionAssert.AreEqual(original.GeneratorPayload, replay.GeneratorPayload);
        Assert.AreEqual(original.Spawn.xf, replay.Spawn.xf);
        Assert.AreEqual(original.Spawn.yf, replay.Spawn.yf);
        Assert.AreEqual(original.Spawn.zf, replay.Spawn.zf);
        Assert.AreEqual(original.Geometry.Doors.Count, replay.Geometry?.Doors.Count);
        Assert.AreEqual(original.FloorTransitions.Count, replay.FloorTransitions.Count);
        Assert.AreEqual(0, replay.Doors?.OpenChancePercent);
        Assert.IsNotNull(replay.RoomSet);

        QuestDungeonParameters newlyAccepted = fixture.Mission();
        Assert.IsTrue(runtimeB.ConfigureNewDungeon(newlyAccepted, team));
        Assert.AreNotEqual(accepted.Content, newlyAccepted.Content);
        Assert.IsTrue(runtimeB.TryGenerate(newlyAccepted, out var changed));
        Assert.AreEqual(100, changed.Doors?.OpenChancePercent);

        string rooms = fixture.RetainedRoomPath(accepted.Content);
        byte[] originalBytes = File.ReadAllBytes(rooms);
        File.Delete(rooms);
        Assert.IsFalse(fixture.CreateGenerator().TryGenerate(restored, out _), "Missing old bytes must not select current geometry.");
        File.WriteAllBytes(rooms, [.. originalBytes, (byte)' ']);
        Assert.IsFalse(fixture.CreateGenerator().TryGenerate(restored, out _), "Changed old bytes must not be accepted.");
    }

    [TestMethod]
    public void InvalidActiveIndexBlocksNewAcceptanceWhileLegacyAndRetainedLayoutsCanBeRestored()
    {
        using var fixture = new RetainedAssets();
        fixture.Publish(doorOpenChance: 42);
        QuestDungeonParameters accepted = fixture.Mission();
        Assert.IsTrue(fixture.CreateGenerator().ConfigureNewDungeon(accepted, team: false));
        DungeonContentReference? reference = accepted.Content;
        Assert.IsNotNull(reference);

        fixture.InvalidateCurrentIndex();
        var runtime = fixture.CreateGenerator();
        Assert.IsFalse(runtime.IsAvailable);
        Assert.IsFalse(runtime.ConfigureNewDungeon(fixture.Mission(), team: false));
        Assert.IsTrue(runtime.TryGenerate(accepted, out var retained));
        Assert.AreEqual(42, retained.Doors?.OpenChancePercent);

        accepted.Content = null;
        Assert.IsTrue(runtime.TryGenerate(accepted, out var legacy));
        Assert.AreEqual(42, legacy.Doors?.OpenChancePercent);
        CollectionAssert.AreEqual(retained.GeneratorPayload, legacy.GeneratorPayload);
        Assert.IsTrue(runtime.TryGenerate(accepted.Seed, 2, accepted.EntranceInstance, out _));

        accepted.Content = reference;
        accepted.ObjectiveNpc = null;
        Assert.IsFalse(QuestDungeonParameters.TryParse(accepted.ToJson(), out _));
        accepted.Content = null;
        Assert.IsTrue(QuestDungeonParameters.TryParse(accepted.ToJson(), out _), "Unbound historical rows remain readable.");
    }

    sealed class RetainedAssets : IDisposable
    {
        readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aorebirth-retained-dungeon-test-");
        readonly string _source = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName)
            ?? throw new AssertFailedException("Original room resources require the established full GameData.");
        readonly byte[] _generation;
        readonly int[] _styles;
        readonly Dictionary<string, MobTemplate> _mobs;

        internal RetainedAssets()
        {
            _generation = File.ReadAllBytes(Path.Combine(_source, DungeonLayoutGenerator.GenerationFileName));
            using JsonDocument policy = JsonDocument.Parse(_generation);
            _styles = policy.RootElement.GetProperty("Profiles").EnumerateArray()
                .SelectMany(row => row.GetProperty("Styles").EnumerateArray().Select(style => style.GetInt32()))
                .Distinct().ToArray();
            _mobs = policy.RootElement.GetProperty("Population").GetProperty("RegularNpcHashes").EnumerateArray()
                .Select(entry => entry.GetString()!).ToDictionary(hash => hash, hash => new MobTemplate { Hash = hash });
        }

        internal QuestDungeonParameters Mission() => new()
        {
            Seed = 12345,
            Quality = 220,
            EntranceInstance = unchecked((int)0xC00001F9),
            DungeonPlayfield = QuestDungeonIds.FirstPlayfield + 101,
            ObjectiveNpc = new MissionNpcObjectiveBinding
            {
                QuestId = "test", OwnerId = 1, Instance = 1, Quality = 220, RoomIndex = 1, NpcHash = _mobs.Keys.First()
            }
        };

        internal DungeonLayoutGenerator CreateGenerator() => new(
            new StubGameData(HashItemCatalog.Parse("{}"), rootPath: _root.FullName, mobs: _mobs), new StubLogger());

        internal void Publish(int doorOpenChance)
        {
            string fileName = DungeonLayoutGenerator.DoorStylesFileName;
            var doorData = JsonNode.Parse(File.ReadAllBytes(Path.Combine(_source, fileName)))!.AsObject();
            doorData["OpenChancePercent"] = doorOpenChance;
            var npcFiles = new Dictionary<string, byte[]>
            {
                ["NpcTemplates.json"] = JsonSerializer.SerializeToUtf8Bytes(_mobs.Keys.ToDictionary(hash => hash,
                    hash => new { Templates = new[] { new { Level = 1, Name = hash } } })),
                ["MonsterCombatRules.json"] = Encoding.UTF8.GetBytes("{}")
            };
            string npcRevision = Identity("AORebirth dungeon NPC content v1\n", npcFiles);
            foreach ((string path, byte[] data) in npcFiles)
                Write($"{RetainedDungeonContent.Folder}/NpcCatalog/{npcRevision}/{path}", data);

            var common = new Dictionary<string, byte[]>
            {
                [DungeonLayoutGenerator.GenerationFileName] = _generation,
                [fileName] = JsonSerializer.SerializeToUtf8Bytes(doorData),
                ["DungeonEntrances.json"] = File.ReadAllBytes(Path.Combine(_source, "DungeonEntrances.json")),
                [GameDataPaths.DynelCollisionFileName] =
                    File.ReadAllBytes(Path.Combine(_source, GameDataPaths.DynelCollisionFileName))
            };
            foreach ((string path, byte[] bytes) in common)
                Write(path, bytes);
            common["NpcCatalog.json"] = JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, Sha256 = npcRevision });

            var hashes = new Dictionary<int, string>();
            foreach (int style in _styles)
            {
                var files = new Dictionary<string, byte[]>(common);
                foreach (string member in new[] { "metadata.json", "Rooms.json", "Districts.json", "Surfaces.dat", "DCGA.png", "GNDA.png" })
                {
                    string path = $"Playfields/{style}/{member}";
                    byte[] content = File.ReadAllBytes(Path.Combine(_source, path));
                    Write(path, content);
                    files.Add(path, content);
                }

                string identity = Identity("AORebirth dungeon content v1\n", files);
                hashes.Add(style, identity);
                foreach ((string path, byte[] bytes) in files)
                    Write($"{RetainedDungeonContent.Folder}/{identity}/{path}", bytes);
            }

            Write(RetainedDungeonContent.CurrentFile, JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, Styles = hashes }));
        }

        internal string RetainedRoomPath(DungeonContentReference reference)
            => Path.Combine(_root.FullName, RetainedDungeonContent.Folder, reference.Sha256, "Playfields",
                reference.StyleId.ToString(System.Globalization.CultureInfo.InvariantCulture), "Rooms.json");

        internal void InvalidateCurrentIndex() => Write(RetainedDungeonContent.CurrentFile, Encoding.UTF8.GetBytes("{}"));

        static string Identity(string header, Dictionary<string, byte[]> files) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(header + string.Concat(files
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key} {Convert.ToHexStringLower(SHA256.HashData(entry.Value))}\n")))));

        void Write(string relative, byte[] data)
        {
            string path = Path.Combine(_root.FullName, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data);
        }

        public void Dispose() => _root.Delete(recursive: true);
    }
}
