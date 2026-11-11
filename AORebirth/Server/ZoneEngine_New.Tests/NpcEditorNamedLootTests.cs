namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using AORebirth.Core.GameData;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.NpcEditor;

[TestClass]
public sealed class NpcEditorNamedLootTests
{
    const int Playfield = 777;
    const string Actor = "NamedLootGM";

    [TestMethod]
    public void NamedTablesAndAssignmentsPersistAndProtectReferencesAndRevisions()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorNamedLoot(data, fixture.Items);
        var stale = new NpcEditorNamedLoot(fixture.Open(), fixture.Items);
        NamedLootTable table = Table("boss");
        editor.SaveTable(new(0, table), Actor);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            stale.SaveTable(new(0, Table("other")), Actor)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.SaveAssignment(new(1, new("wrong", "namedBoss", 0, "append", "boss", TemplateHash: "NPC2")), Actor)).StatusCode);
        var assignment = new NamedLootAssignment("boss-loot", "namedBoss", 0, "append", "boss", TemplateHash: "NPC1");
        editor.PreviewAssignment(new(1, assignment));
        Assert.AreEqual(1, editor.Describe().Revision);
        var assigned = editor.SaveAssignment(new(1, assignment), Actor);
        Assert.AreEqual(Actor, assigned.History[^1].Actor);
        Assert.AreEqual(409, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.DeleteTable(new(2, "boss"), Actor)).StatusCode);
        Assert.IsTrue(data.ReloadNpcTemplates().Ok);

        GameDataStore reopened = fixture.Open();
        var restarted = new NpcEditorNamedLoot(reopened, fixture.Items);
        NamedLootRegistryState state = restarted.Describe();
        Assert.AreEqual(2, state.Revision);
        var registry = new NamedLootRegistry(state.Tables, state.Assignments);
        Assert.AreEqual("boss/main", registry.Resolve(new(20, Playfield, "NPC1", IsBoss: true)).Groups.Single().QualifiedId);
        Assert.AreEqual(0, registry.Resolve(new(20, Playfield, "NPC1")).Groups.Length);
        Assert.AreEqual(0, registry.Resolve(new(20, Playfield, "NPC1", IsBoss: true, IsOwnedSummon: true)).Groups.Length);
        Assert.AreEqual(0, registry.Resolve(new(20, Playfield, "NPC2", IsBoss: true)).Groups.Length);

        NamedLootRegistryState removed = restarted.Restore(new(2, 1), Actor);
        Assert.AreEqual(3, removed.Revision);
        Assert.AreEqual(0, removed.Assignments.Length);
        Assert.AreEqual(0, new NpcEditorNamedLoot(fixture.Open(), fixture.Items).Describe().Assignments.Length);
        Assert.AreEqual(fixture.OriginalTemplates, File.ReadAllText(fixture.TemplatePath));
    }

    [TestMethod]
    public void InheritanceKeepsStableGroupIdentityAndScopesAuthoredPointByItsSignature()
    {
        using var fixture = new Fixture();
        GameDataStore data = fixture.Open();
        var editor = new NpcEditorNamedLoot(data, fixture.Items);
        editor.SaveTable(new(0, Table("baseline")), Actor);
        editor.SaveTable(new(1, Table("replace")), Actor);
        editor.SaveAssignment(new(2, new("default", "global", 0, "append", "baseline")), Actor);
        editor.SaveAssignment(new(3, new("replacement", "family", 0, "replace", "replace", FamilyHash: "FAM1",
            TargetGroup: "baseline/main", SourceGroupId: "main", Condition: new(MinimumLevel: 15))), Actor);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.SaveAssignment(new(4, new("ambiguous", "family", 0, "suppress", FamilyHash: "FAM1",
                TargetGroup: "baseline/main")), Actor)).StatusCode);

        PlayfieldSpawnEntry point = data.ReadAuthoredEditorSpawns(Playfield).Spawns[0];
        string signature = NpcEditorSpawnStore.Signature(point);
        NamedLootRegistryState saved = editor.SaveAssignment(new(4, new("point", "authoredPoint", 0, "suppress",
            PlayfieldId: Playfield, AuthoredIndex: 0, AuthoredSignature: signature, TargetGroup: "baseline/main")), Actor);
        var registry = new NamedLootRegistry(saved.Tables, saved.Assignments);
        var matching = new NamedLootContext(20, Playfield, "NPC1", 0, ["FAM1"], 0, signature);
        NamedLootResolution suppressed = registry.Resolve(matching);
        Assert.AreEqual(0, suppressed.Groups.Length);
        CollectionAssert.AreEqual(new[] { "append", "replace", "suppress" }, suppressed.Trace.Select(note => note.Action).ToArray());

        NamedLootResolvedGroup replaced = registry.Resolve(matching with { AuthoredSignature = "wrong" }).Groups.Single();
        Assert.AreEqual("baseline/main", replaced.QualifiedId);
        Assert.AreEqual("replace", replaced.TableId);
        Assert.AreEqual("baseline", registry.Resolve(matching with { Level = 10, AuthoredIndex = 1 }).Groups.Single().TableId);
        Assert.AreEqual("replace", registry.Resolve(matching with { PlayfieldId = 778 }).Groups.Single().TableId);
    }

    [TestMethod]
    public void UnsupportedEvidenceAndInvalidItemWindowsCannotActivate()
    {
        using var fixture = new Fixture();
        var editor = new NpcEditorNamedLoot(fixture.Open(), fixture.Items);
        NamedLootTable table = Table("bad");
        NamedLootGroup group = table.Groups[0];
        NamedLootEntry item = group.Entries[0];
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() => editor.SaveTable(
            new(0, table with { Groups = [group with { Entries = [item with { MinQuality = 30, MaxQuality = 40 }] }] }),
            Actor)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() => editor.SaveTable(
            new(0, table with { Groups = [group with { Entries = [item with
            {
                EvidenceClassification = "OBSERVED_AVAILABLE_LOOT", EvidenceReference = "one observed corpse"
            }] }] }), Actor)).StatusCode);
        Assert.AreEqual(0, editor.Describe().Revision);
        Assert.IsFalse(File.Exists(fixture.NamedLootPath));

        var observed = item with { Weight = 5, EvidenceClassification = "OBSERVED_AVAILABLE_LOOT", EvidenceReference = "corpse one" };
        var weighted = group with { Mode = "WEIGHTED_ONE", Entries = [observed] };
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.SaveTable(new(0, table with { Groups = [weighted] }), Actor)).StatusCode);
        Assert.AreEqual(400, Assert.ThrowsExactly<NpcEditorRuntimeException>(() =>
            editor.SaveTable(new(0, table with { Groups = [group with
            {
                Entries = [item with { EvidenceClassification = "GUARANTEED_PROVEN", EvidenceReference = "unverified claim" }]
            }] }), Actor)).StatusCode);
        Assert.AreEqual(1, editor.SaveTable(new(0, table with { Groups = [weighted with { EmptyWeight = 1 }] }), Actor).Revision);
    }

    static NamedLootTable Table(string id) => new(id, id, string.Empty, "namedBoss",
        [new("main", "GUARANTEED_ALL", 1, 10_000, 0, null,
            [new("item", 9001, 9001, 25, 25, false, 1, 1, 10_000, 0, false)])]);

    sealed class Fixture : IDisposable
    {
        readonly string _root = Directory.CreateTempSubdirectory("aorebirth-named-loot-").FullName;
        internal const string Templates = """
            {
              "FAM1":{"Children":["NPC1","NPC2"]},
              "NPC1":{"Templates":[{"Name":"Named Boss","Level":20,"CharacterFlags":2}]},
              "NPC2":{"Templates":[{"Name":"Ordinary NPC","Level":20}]}
            }
            """;

        internal Fixture()
        {
            string spawns = Path.Combine(GameData, GameDataPaths.PlayfieldSpawnsRelativePath(Playfield));
            Directory.CreateDirectory(Path.GetDirectoryName(spawns)!);
            Directory.CreateDirectory(Overlay);
            File.WriteAllText(TemplatePath, Templates);
            File.WriteAllText(spawns, JsonSerializer.Serialize(new PlayfieldSpawnsData
            {
                SchemaVersion = PlayfieldSpawnsData.SupportedSchemaVersion, PlayfieldId = Playfield,
                Spawns = [new PlayfieldSpawnEntry
                {
                    HashText = "NPC1", Position = [1, 0, 1], MinLevel = 20, MaxLevel = 20,
                    RespawnChance = 100, RespawnTime = 20
                }]
            }));
        }

        string GameData => Path.Combine(_root, "GameData");
        string Overlay => Path.Combine(_root, "Overlay");
        internal string OriginalTemplates => Templates;
        internal string TemplatePath => Path.Combine(GameData, GameDataPaths.NpcTemplatesFileName);
        internal string NamedLootPath => Path.Combine(Overlay, NamedLootJournalFile.FileName);
        internal StubCatalog Items { get; } = new StubCatalog().Add(9001, 25);
        internal GameDataStore Open() => new(new StubLogger(), null, GameData, Overlay);
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
