namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text.Json;

using AORebirth.NpcEditor.Official;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class NpcEditorOfficialResearchTests
{
    [TestMethod]
    public void MultipleFreshClientsRequireSelectionAndLosingOneNeverSwitchesToAnother()
    {
        using var fixture = new Fixture();
        OfficialSnapshot first = fixture.Snapshot(pid: 111);
        OfficialSnapshot second = fixture.Snapshot(pid: 222);
        fixture.Publish(first);
        fixture.Publish(second);
        var reader = fixture.Reader();

        OfficialReadResult choose = reader.Read();
        Assert.AreEqual("chooseSession", choose.Status);
        Assert.AreEqual(2, choose.Sessions.Length);
        Assert.IsNull(choose.Snapshot);
        var requested = new OfficialSessionKey(second.ProcessId, second.SessionId);
        Assert.AreEqual(second.InstanceId, reader.Read(requested).Snapshot!.InstanceId);
        File.Delete(fixture.PathFor(second));

        OfficialReadResult lost = reader.Read(requested);
        Assert.AreEqual("sessionUnavailable", lost.Status);
        Assert.AreEqual(requested, lost.ActiveSession);
        Assert.IsNull(lost.Snapshot);
        Assert.AreEqual(first.ProcessId, lost.Sessions[0].Key.ProcessId);
    }

    [TestMethod]
    public void ZoningInvalidatesTheLockedGenerationUntilTheUserUnlocks()
    {
        using var fixture = new Fixture();
        var reader = fixture.Reader();
        OfficialSnapshot selected = fixture.Snapshot();
        fixture.Publish(selected);
        OfficialSelectionLock targetLock = OfficialSelectionLock.From(reader.Read().Snapshot!);

        fixture.Publish(new OfficialSnapshot
        {
            ProcessId = selected.ProcessId, SessionId = selected.SessionId,
            CapturedUtcTicks = selected.CapturedUtcTicks, Sequence = 2, State = "zoning"
        });
        OfficialReadResult zoned = reader.Read(targetLock.Session, targetLock);
        Assert.IsTrue(zoned.LockInvalidated);
        Assert.IsNull(zoned.Snapshot);
        Assert.AreEqual("lockedTargetUnavailable", zoned.Status);

        selected.Sequence = 3;
        fixture.Publish(selected);
        Assert.IsTrue(reader.Read(targetLock.Session, targetLock).LockInvalidated);
        Assert.AreEqual("selected", reader.Read(targetLock.Session).Status);

        selected.Sequence = 4;
        selected.SelectionId = Guid.NewGuid().ToString("N");
        fixture.Publish(selected);
        Assert.IsTrue(reader.Read(targetLock.Session, targetLock).LockInvalidated,
            "Reusing the same AO identity does not restore a lock on its earlier selection.");
    }

    [TestMethod]
    public void StaleFutureMalformedAndReplayedSnapshotsFailClosed()
    {
        using var fixture = new Fixture();
        var reader = fixture.Reader();
        OfficialSnapshot snapshot = fixture.Snapshot();
        string path = fixture.PathFor(snapshot);
        var session = new OfficialSessionKey(snapshot.ProcessId, snapshot.SessionId);
        fixture.Publish(snapshot);
        Assert.AreEqual("selected", reader.Read(session).Status);

        snapshot.Name = "A changed payload with the same sequence";
        fixture.Publish(snapshot);
        Assert.AreEqual("sessionUnavailable", reader.Read(session).Status);
        snapshot.Sequence = 2;
        snapshot.CapturedUtcTicks = fixture.Clock.Now.AddSeconds(-5).UtcTicks;
        fixture.Publish(snapshot);
        Assert.IsNull(reader.Read(session).Snapshot);
        snapshot.CapturedUtcTicks = fixture.Clock.Now.AddSeconds(2).UtcTicks;
        fixture.Publish(snapshot);
        Assert.IsNull(reader.Read(session).Snapshot);
        snapshot.CapturedUtcTicks = fixture.Clock.Now.UtcTicks;
        fixture.Publish(snapshot);
        Assert.AreEqual("selected", reader.Read(session).Status);

        snapshot.Sequence = 1;
        fixture.Publish(snapshot);
        Assert.IsNull(reader.Read(session).Snapshot);
        File.WriteAllText(path, "{\"Version\":2}");
        Assert.AreEqual("sessionUnavailable", reader.Read(session).Status);
        File.WriteAllBytes(path, new byte[128 * 1024 + 1]);
        Assert.AreEqual("invalid", reader.Read().Status);
    }

    [TestMethod]
    public void FileIdentityMustMatchAndUnknownFieldsRemainUnknown()
    {
        using var fixture = new Fixture();
        var reader = fixture.Reader();
        OfficialSnapshot snapshot = fixture.Snapshot();
        snapshot.Fields =
        [
            new OfficialField { Key = "health.current", Value = null, Source = "AOSharp stat availability",
                Classification = "NOT_AVAILABLE" }
        ];
        fixture.Publish(snapshot);
        OfficialField unknown = reader.Read().Snapshot!.Fields[0];
        Assert.IsNull(unknown.Value);
        Assert.AreEqual("NOT_AVAILABLE", unknown.Classification);
        File.Move(fixture.PathFor(snapshot), Path.Combine(fixture.Root, $"research-999-{snapshot.SessionId}.v2.json"));
        Assert.AreEqual("invalid", reader.Read().Status);
    }

    [TestMethod]
    public void ExportPreservesSnapshotEventsAndManualLootWithoutMixingGenerations()
    {
        using var fixture = new Fixture();
        OfficialSnapshot snapshot = fixture.Snapshot();
        snapshot.Fields =
        [
            new OfficialField { Key = "equipment.armor", Source = "AOSharp passive inspection",
                Classification = "NOT_AVAILABLE" }
        ];
        snapshot.Events =
        [
            new OfficialEvent
            {
                CapturedUtcTicks = snapshot.CapturedUtcTicks - TimeSpan.TicksPerSecond,
                Kind = "movement", Detail = "Moved, then \"stopped\".", Source = "AOSharp position",
                Classification = "DERIVED_FROM_OBSERVATIONS"
            }
        ];
        var manual = new OfficialLootObservation(OfficialSelectionLock.From(snapshot), snapshot.CapturedUtcTicks, "UNRESOLVED_ITEM",
            CorpseIdentity: "51050:222", ItemName: "=sample, \"item\"", Quantity: 2, Notes: "Seen in a manually opened corpse.");
        string exports = Path.Combine(fixture.Root, "Exports");

        OfficialExportPaths saved = OfficialObservationExport.WriteAt(exports, snapshot, [manual]);
        Assert.AreEqual(exports, Path.GetDirectoryName(saved.JsonPath));
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(saved.JsonPath));
        JsonElement root = json.RootElement;
        Assert.AreEqual("officialAOReadOnly", root.GetProperty("mode").GetString());
        Assert.AreEqual(snapshot.CapturedUtcTicks, root.GetProperty("snapshot").GetProperty("capturedUtcTicks").GetInt64());
        Assert.AreEqual("NOT_AVAILABLE",
            root.GetProperty("snapshot").GetProperty("fields")[0].GetProperty("classification").GetString());
        JsonElement loot = root.GetProperty("manualLoot")[0];
        Assert.AreEqual(manual.ItemName, loot.GetProperty("itemName").GetString());
        Assert.AreEqual("UNRESOLVED_ITEM", loot.GetProperty("status").GetString());
        Assert.AreEqual("GAMEPLAY_OBSERVED", loot.GetProperty("classification").GetString());
        string csv = File.ReadAllText(saved.CsvPath);
        StringAssert.Contains(csv, "\"Moved, then \"\"stopped\"\".\"");
        StringAssert.Contains(csv, "\"'=sample, \"\"item\"\"\"");
        StringAssert.Contains(csv, "Manual official AO loot observation");

        var other = manual with { Target = manual.Target with { SelectionId = Guid.NewGuid().ToString("N") } };
        Assert.ThrowsExactly<ArgumentException>(() => OfficialObservationExport.WriteAt(exports, snapshot, [other]));
        Assert.AreEqual(2, Directory.GetFiles(exports).Length);
        Assert.ThrowsExactly<IOException>(() => OfficialObservationExport.WriteAt(
            @"\\invalid.example\research-exports", snapshot, [manual]));
    }

    sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("npc-editor-official-").FullName;
        internal TestTime Clock { get; } = new();
        internal OfficialSnapshotReader Reader() => new(Root, Clock);

        internal OfficialSnapshot Snapshot(int pid = 123) => new()
        {
            ProcessId = pid,
            SessionId = Guid.NewGuid().ToString("N"),
            SelectionId = Guid.NewGuid().ToString("N"),
            CapturedUtcTicks = Clock.Now.UtcTicks,
            State = "selected",
            Sequence = 1,
            PlayfieldId = 4582,
            IdentityType = 50000,
            InstanceId = pid * 10,
            Name = "Fixture NPC",
            MonsterData = 123,
            Level = 10
        };

        internal string PathFor(OfficialSnapshot snapshot) => Path.Combine(Root,
            FormattableString.Invariant($"research-{snapshot.ProcessId}-{snapshot.SessionId}.v2.json"));

        internal void Publish(OfficialSnapshot snapshot)
        {
            using var file = File.Create(PathFor(snapshot));
            new DataContractJsonSerializer(typeof(OfficialSnapshot)).WriteObject(file, snapshot);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    sealed class TestTime : TimeProvider
    {
        internal DateTimeOffset Now { get; } = new(2026, 11, 11, 18, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
