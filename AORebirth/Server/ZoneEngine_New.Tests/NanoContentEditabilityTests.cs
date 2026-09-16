namespace ZoneEngine_New.Tests;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Nanos;

[TestClass]
public sealed class NanoContentEditabilityTests
{
    [TestMethod]
    public void LoadedNanoBindingAndStatOverlayChangeWithoutRecompile()
    {
        using var file = new ContentFile();
        var definition = new NanoMechanicDefinition { NanoId = 223767, Kind = NanoMechanicKind.ActiveStatOverlay,
            Modifiers = new() { [CharacterStat.MapsC] = 7 } };
        file.Write(definition);
        byte[] before = SHA256.HashData(File.ReadAllBytes(typeof(NanoMechanicCatalog).Assembly.Location));
        var first = new ActiveStatOverlayNanoSpecialization(NanoMechanicCatalog.Load(file.Path));
        Assert.IsTrue(first.Handles(223767));
        Assert.AreEqual(7, first.Project([Active(223767)])[CharacterStat.MapsC]);
        definition.NanoId = 223768;
        definition.Modifiers[CharacterStat.MapsC] = 31;
        file.Write(definition);
        var second = new ActiveStatOverlayNanoSpecialization(NanoMechanicCatalog.Load(file.Path));
        Assert.IsFalse(second.Handles(223767));
        Assert.IsTrue(second.Handles(223768));
        Assert.AreEqual(31, second.Project([Active(223768)])[CharacterStat.MapsC]);
        CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(typeof(NanoMechanicCatalog).Assembly.Location)));
    }

    [TestMethod]
    public void HealTiersAndPlayfieldRestrictionsComeFromReloadedContent()
    {
        using var file = new ContentFile();
        var heal = new NanoMechanicDefinition { NanoId = 302365, Kind = NanoMechanicKind.PeriodicTeamHeal,
            PulseSeconds = 20, HealTiers = [new() { MinimumLevel = 0, NanoId = 300495, Amount = 10 }] };
        var teleport = new NanoMechanicDefinition { NanoId = 154914, Kind = NanoMechanicKind.TeamTeleport,
            ExcludedPlayfields = [127] };
        file.Write(heal, teleport);
        var first = NanoMechanicCatalog.Load(file.Path);
        Assert.AreEqual((300495, 10), (first.Get(heal.NanoId, heal.Kind).HealTiers.Single().NanoId, first.Get(heal.NanoId, heal.Kind).HealTiers.Single().Amount));
        CollectionAssert.AreEqual(new[] { 127 }, first.Get(teleport.NanoId, teleport.Kind).ExcludedPlayfields);
        heal.HealTiers[0].NanoId = 300497; heal.HealTiers[0].Amount = 143; teleport.ExcludedPlayfields = [];
        file.Write(heal, teleport);
        var second = NanoMechanicCatalog.Load(file.Path);
        Assert.AreEqual((300497, 143), (second.Get(heal.NanoId, heal.Kind).HealTiers.Single().NanoId, second.Get(heal.NanoId, heal.Kind).HealTiers.Single().Amount));
        Assert.AreEqual(0, second.Get(teleport.NanoId, teleport.Kind).ExcludedPlayfields.Length);
    }

    [TestMethod]
    public void ProvenanceDoesNotGrantOrDenyMechanicActivation()
    {
        var definition = new NanoMechanicDefinition { NanoId = 17, Kind = NanoMechanicKind.DurationOnly };
        Assert.IsTrue(new DurationOnlyNanoSpecialization(new([definition])).Handles(17));
        definition.Provenance = "unreviewed operator-authored content";
        Assert.IsTrue(new DurationOnlyNanoSpecialization(new([definition])).Handles(17));
    }

    [TestMethod]
    public void DuplicateBindingsAndInvalidRangesAreRejected()
    {
        var definition = new NanoMechanicDefinition { NanoId = 17, Kind = NanoMechanicKind.DurationOnly };
        Assert.ThrowsException<InvalidDataException>(() => new NanoMechanicCatalog([definition, definition]));
        definition.ExcludedPlayfieldRanges = [[5, 4]];
        Assert.ThrowsException<InvalidDataException>(() => new NanoMechanicCatalog([definition]));
    }

    [TestMethod]
    public void Null_fields_and_unknown_teleport_modes_are_rejected_during_load()
    {
        using var file = new ContentFile();
        foreach (string field in new[] { "Modifiers", "ChildNanoIds", "ExcludedPlayfields", "ExcludedPlayfieldRanges", "HealTiers" })
        {
            File.WriteAllText(file.Path, "[{\"NanoId\":17,\"Kind\":\"DurationOnly\",\"" + field + "\":null}]");
            Assert.ThrowsException<InvalidDataException>(() => NanoMechanicCatalog.Load(file.Path), field);
        }
        Assert.ThrowsException<InvalidDataException>(() => new NanoMechanicCatalog([new()
            { NanoId = 17, Kind = NanoMechanicKind.TeamTeleport, RecipientMode = (TeleportRecipientMode)99 }]));
    }

    [TestMethod]
    public void Transient_bitmask_projection_cannot_overwrite_durable_actor_stats()
    {
        foreach (var stat in new[] { CharacterStat.Cash, CharacterStat.GmLevel, CharacterStat.Level, CharacterStat.Health })
            Assert.ThrowsException<InvalidDataException>(() => new NanoMechanicCatalog([new()
                { NanoId = 17, Kind = NanoMechanicKind.ActiveStatOverlay, Modifiers = new() { [stat] = 7 } }]), stat.ToString());
        Assert.ThrowsException<InvalidDataException>(() => new NanoMechanicCatalog([new()
            { NanoId = 17, Kind = NanoMechanicKind.ActiveStatOverlay, Modifiers = new() { [CharacterStat.MapsC] = -1 } }]));
        Assert.IsTrue(new NanoMechanicCatalog([new() { NanoId = 17, Kind = NanoMechanicKind.ActiveStatOverlay,
            Modifiers = new() { [CharacterStat.MapsC] = 31 } }]).TryGet(17, NanoMechanicKind.ActiveStatOverlay, out _));
    }

    [TestMethod]
    [DataRow("hex")] [DataRow("actor")] [DataRow("duplicate-offset")] [DataRow("block")] [DataRow("count")]
    public void Invalid_visual_content_is_rejected_before_any_cast(string invalid)
    {
        var definition = NanoMechanicCatalog.LoadDefault().Get(82835, NanoMechanicKind.Morph);
        var visual = definition.ApplyVisual!;
        if (invalid == "hex") visual.Hex = "invalid";
        if (invalid == "actor") visual.SourceActorId++;
        if (invalid == "duplicate-offset") visual.ActorOffsets = [12, 12];
        if (invalid == "block") visual.FlightBlock!.Offset++;
        if (invalid == "count") visual.FlightBlock!.CountWithBlock++;
        Assert.ThrowsException<InvalidDataException>(() => new NanoMechanicCatalog([definition]));
    }

    private static ActiveNanoRecord Active(int id) => new(id, 1, 1, 100, DateTime.UtcNow.AddMinutes(1).Ticks);
    private sealed class ContentFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nano-content-" + Guid.NewGuid().ToString("N") + ".json");
        public void Write(params NanoMechanicDefinition[] definitions) => File.WriteAllText(Path, JsonSerializer.Serialize(definitions));
        public void Dispose() => File.Delete(Path);
    }
}
