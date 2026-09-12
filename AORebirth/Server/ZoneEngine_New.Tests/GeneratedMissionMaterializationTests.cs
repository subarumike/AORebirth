namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Interfaces.Persistence.Missions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Missions;
using ZoneEngine.Core.Packets;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Mobs;
using Vector3 = AORebirth.Core.Vector.Vector3;

[TestClass]
public sealed class GeneratedMissionMaterializationTests
{
    static readonly DateTime Accepted = new(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void AllFiveAcceptedBundlesMaterializeThroughActualNpcFactoryWithExactIdentityAndLife()
    {
        var bundles = MissionAcgCapturedLayoutCatalog.CreateBundles();
        Assert.AreEqual(5, bundles.Length);
        int index = 0, passiveObjectives = 0;
        foreach (var bundle in bundles)
        {
            var (binding, materialized, objects) = Prepare(bundle, index++);
            var world = World(binding, materialized, objects);
            var dynels = world.CreateDynels(null!, new StubItemBuilder());
            Assert.AreEqual(materialized.Objects.Count, dynels.Count, bundle.LayoutId);
            CollectionAssert.AreEquivalent(objects.Select(row => row.RuntimeType + ":" + row.RuntimeInstance).ToArray(),
                dynels.Select(dynel => (int)dynel.Identity.Type + ":" + dynel.Identity.Instance).ToArray());
            foreach (var npc in dynels.OfType<GeneratedMissionNpcCharacter>())
            {
                var row = objects.Single(value => value.RuntimeInstance == npc.Identity.Instance && value.RuntimeType == (int)npc.Identity.Type);
                Assert.AreEqual(row.Level, npc.Stats.GetOrZero(CharacterStat.Level));
                Assert.AreEqual(row.CurrentHealth, npc.Stats.GetOrZero(CharacterStat.Health));
                Assert.AreEqual(row.MaxHealth, npc.Stats.GetOrZero(CharacterStat.MaxHealth));
                var source = materialized.Objects.Single(value => value.Identity.RuntimeIdentity.Instance == npc.Identity.Instance && value.Identity.RuntimeIdentity.Type == (int)npc.Identity.Type);
                var evidence = new GeneratedMissionNpcEvidence(source, bundle, binding.Offer.Quality, binding.Offer.MissionType);
                Assert.AreEqual(bundle.SourcePlayfield2, evidence.SourcePlayfield2);
                Assert.AreEqual(source.Identity.CapturedIdentity.Instance, evidence.CapturedInstance);
                if (evidence.IsFindPerson) { Assert.IsNull(npc.Combat); passiveObjectives++; }
                else { Assert.IsNotNull(npc.Combat); Assert.IsTrue(npc.Combat.Contract.IsCombatReady); }
            }
            var packet = world.CreateZoneMessage(new() { X = world.Spawn.xf, Y = world.Spawn.yf, Z = world.Spawn.zf });
            Assert.AreEqual(binding.LivePlayfield, packet.PlayfieldId2.Instance);
            Assert.AreEqual(binding.BuildingType, (int)packet.PlayfieldId1.Type);
            Assert.AreEqual(binding.BuildingInstance, packet.PlayfieldId1.Instance);
            CollectionAssert.AreEqual(bundle.CopyGeneratorPayload(), packet.GeneratorPayload);
            Assert.IsTrue(world.ContainsPosition(world.Spawn));
            Assert.IsFalse(world.ContainsPosition(new Vector3(float.NaN, 0, 0)));
            Assert.IsFalse(world.ContainsPosition(new Vector3(float.MaxValue, 0, 0)));
        }
        Assert.IsTrue(passiveObjectives > 0, "The actual FindPerson bundle must exercise the passive target path.");
    }

    [TestMethod]
    public void RecreatedWorldPreservesDamagedMovedAndDeadNpcStateInsteadOfRespawningDefaults()
    {
        foreach (var pair in MissionAcgCapturedLayoutCatalog.CreateBundles().Select((bundle, index) => (bundle, index)))
        {
            var (binding, materialized, objects) = Prepare(pair.bundle, pair.index);
            var npcs = objects.Where(row => row.CurrentHealth.HasValue).ToArray();
            Assert.IsTrue(npcs.Length >= 2, pair.bundle.LayoutId);
            npcs[0].CurrentHealth = 37; npcs[0].X += 0.25f; npcs[0].Version = 9;
            npcs[1].CurrentHealth = 0; npcs[1].IsDead = true; npcs[1].Version = 4;
            var dynels = World(binding, materialized, objects).CreateDynels(null!, new StubItemBuilder());
            var restored = (NpcCharacter)dynels.Single(value => value.Identity.Instance == npcs[0].RuntimeInstance);
            Assert.AreEqual(37, restored.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(npcs[0].X, restored.Position.xf);
            Assert.IsFalse(dynels.Any(value => value.Identity.Instance == npcs[1].RuntimeInstance));
            Assert.AreEqual(9L, npcs[0].Version);
        }
    }

    [TestMethod]
    public void EveryAcceptedMissionNpcProducesExactLegacyCorpseBytesFromDurableDeath()
    {
        foreach (var pair in MissionAcgCapturedLayoutCatalog.CreateBundles().Select((bundle, index) => (bundle, index)))
        {
            var (binding, materialized, objects) = Prepare(pair.bundle, pair.index);
            foreach (var row in objects.Where(value => value.CurrentHealth.HasValue))
            {
                var source = materialized.Objects.Single(value => value.Identity.RuntimeIdentity.Instance == row.RuntimeInstance
                    && value.Identity.RuntimeIdentity.Type == row.RuntimeType);
                var evidence = new GeneratedMissionNpcEvidence(source, pair.bundle, binding.Offer.Quality, binding.Offer.MissionType);
                if (evidence.IsFindPerson) continue;
                var receiver = new Identity { Type = IdentityType.CanbeAffected, Instance = binding.OwnerId };
                Assert.ThrowsException<InvalidOperationException>(() => GeneratedMissionCorpseProjection.BuildPacket(evidence, row, binding.LivePlayfield, receiver));
                row.IsDead = true; row.CurrentHealth = 0; row.CorpseCredits = 29;
                row.X += 0.5f;
                var corpse = GeneratedMissionCorpseProjection.BuildPacket(evidence, row, binding.LivePlayfield, receiver);
                Assert.AreEqual((int)IdentityType.Corpse, BinaryPrimitives.ReadInt32BigEndian(corpse.AsSpan(20, 4)));
                Assert.AreEqual(row.RuntimeInstance, BinaryPrimitives.ReadInt32BigEndian(corpse.AsSpan(24, 4)));
                Assert.AreEqual(binding.OwnerId, BinaryPrimitives.ReadInt32BigEndian(corpse.AsSpan(12, 4)));
                Assert.AreEqual(binding.LivePlayfield, BinaryPrimitives.ReadInt32BigEndian(corpse.AsSpan(73, 4)));
                Assert.AreEqual(row.X, BinaryPrimitives.ReadSingleBigEndian(corpse.AsSpan(45, 4)));
                int nameLength = BinaryPrimitives.ReadInt32BigEndian(corpse.AsSpan(235, 4));
                Assert.AreEqual("Remains of " + evidence.Name, System.Text.Encoding.ASCII.GetString(corpse, 239, nameLength - 1));
                Assert.AreEqual(0, corpse[239 + nameLength - 1]);
                Assert.AreEqual(29, BinaryPrimitives.ReadInt32BigEndian(corpse.AsSpan(207, 4)));
                int catMesh = GeneratedMissionCorpseWire.MissionCatMeshMappings().Where(pair => pair.Key == evidence.MonsterData)
                    .Select(pair => pair.Value).SingleOrDefault();
                var expected = GeneratedMissionCorpseWire.Build(evidence.Name, row.RuntimeInstance, row.RuntimeInstance,
                    binding.OwnerId, binding.LivePlayfield, row.X, row.Y, row.Z, binding.LivePlayfield,
                    evidence.CopySpawnMessage().MonsterScale, 2, 1, 1, catMesh, evidence.MonsterData, 29);
                CollectionAssert.AreEqual(expected.AsSpan(16).ToArray(), corpse.AsSpan(16).ToArray());
                Assert.AreEqual(0xDFDF, BinaryPrimitives.ReadUInt16BigEndian(corpse.AsSpan(0, 2)));
                row.CorpseClaimed = true;
                corpse = GeneratedMissionCorpseProjection.BuildPacket(evidence, row, binding.LivePlayfield, receiver);
                Assert.AreEqual(0, BinaryPrimitives.ReadInt32BigEndian(corpse.AsSpan(207, 4)));
            }
        }
    }

    [TestMethod]
    public void EveryBundleRestoresOnlyUnclaimedUnexpiredExactCorpseWithoutNpcRespawn()
    {
        foreach (var pair in MissionAcgCapturedLayoutCatalog.CreateBundles().Select((bundle, index) => (bundle, index)))
        {
            var (binding, materialized, objects) = Prepare(pair.bundle, pair.index);
            var state = objects.First(value => value.CurrentHealth.HasValue);
            Assert.IsTrue(MissionAcgCorpseCreditPolicy.TryResolve(state.RuntimeInstance, binding.LivePlayfield, out int credits));
            Assert.IsTrue(credits >= 21 && credits <= 87);
            Assert.IsFalse(MissionAcgCorpseCreditPolicy.TryResolve(state.RuntimeInstance, binding.LivePlayfield + 1, out _));
            state.CurrentHealth = 0; state.IsDead = true; state.DeathActorId = binding.OwnerId;
            state.DiedAtUtcTicks = DateTime.UtcNow.AddSeconds(-1).Ticks; state.CorpseExpiresAtUtcTicks = state.DiedAtUtcTicks + TimeSpan.TicksPerMillisecond * 60600; state.CorpseCredits = credits;
            var world = World(binding, materialized, objects);
            var corpse = world.CreateDynels(null!, new StubItemBuilder()).Single(value => value.Identity.Instance == state.RuntimeInstance);
            Assert.IsInstanceOfType<GeneratedMissionCorpseDynel>(corpse);
            Assert.AreEqual(IdentityType.Corpse, corpse.Identity.Type);
            byte[] wire = corpse.BuildSpawnPacket(new() { Type = IdentityType.CanbeAffected, Instance = binding.OwnerId })!;
            Assert.AreEqual((int)IdentityType.Corpse, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(wire.AsSpan(20, 4)));
            Assert.AreEqual(corpse.Identity.Instance, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(wire.AsSpan(24, 4)));
            Assert.AreEqual(binding.LivePlayfield, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(wire.AsSpan(73, 4)));
            Assert.AreEqual(credits, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(wire.AsSpan(207, 4)));
            state.CorpseClaimed = true;
            Assert.IsNull(world.CreateCorpse(state, null!));
            state.CorpseClaimed = false; state.CorpseExpiresAtUtcTicks = DateTime.UtcNow.AddSeconds(-1).Ticks;
            Assert.IsNull(world.CreateCorpse(state, null!));
        }
        Assert.IsFalse(MissionAcgCorpseCreditPolicy.TryResolveSignedSalt(1, int.MaxValue, uint.MaxValue, out _));
    }

    [TestMethod]
    public void ForeignOrIncompletePersistedObjectSetCannotExposeAPartialWorld()
    {
        var (binding, materialized, objects) = Prepare(MissionAcgCapturedLayoutCatalog.CreateBundles()[0], 0);
        var missing = objects.Skip(1).ToList();
        Assert.ThrowsException<InvalidOperationException>(() => World(binding, materialized, missing));
        objects[0].CapturedInstance++;
        Assert.ThrowsException<InvalidOperationException>(() => World(binding, materialized, objects));
    }

    static GeneratedMissionWorld World(GeneratedMissionBinding binding, MissionAcgMaterializedInstance materialized, IList<GeneratedMissionObject> objects)
        => new(binding, materialized, objects, new GeneratedMissionNpcFactory(new StubCatalog(),
            new Lazy<GeneratedMissionAcgService>(() => throw new AssertFailedException("Materialization must not execute combat or write SQL."))), (_, _) => false);

    static (GeneratedMissionBinding, MissionAcgMaterializedInstance, IList<GeneratedMissionObject>) Prepare(MissionAcgLayoutBundle bundle, int index)
    {
        int pf = MissionAcgIdentityRanges.MinimumLivePlayfield2 + 500 + index;
        var type = bundle.CompatibleMissionTypes[0];
        var binding = new GeneratedMissionBinding
        {
            OwnerId = 99, QuestType = 0xDAC3, QuestInstance = 0x50001000 + index, OfferType = 0xDAC3, OfferInstance = 0x55569000 + index,
            KeyInstance = 10000 + index, BundleId = bundle.LayoutId, BundleSha256 = bundle.GeneratorPayloadSha256,
            BuildingType = bundle.BuildingIdentity.Type, BuildingInstance = bundle.BuildingIdentity.Instance, LivePlayfield = pf,
            AcceptedAtUtcTicks = Accepted.Ticks, ExpiresAtUtcTicks = Accepted.AddHours(48).Ticks, State = GeneratedMissionState.Active,
            Offer = new GeneratedMissionOffer { MissionType = (int)type, Quality = 25, DestinationPlayfield = 710, DestinationX = 111, DestinationY = 5, DestinationZ = 222 }
        };
        var immutable = new MissionAcgInstanceBinding(MissionAcgInstanceBinding.CurrentFormatVersion,
            new(binding.QuestType, binding.QuestInstance), new(binding.OfferType, binding.OfferInstance), new(50000, 99), null,
            type, 25, 1234, new(0xC76D, binding.KeyInstance), new(0xC9C6, 710), 123, 124, 111, 5, 222, new(0xDAC1, 100),
            bundle.LayoutId, bundle.GeneratorPayloadSha256, bundle.BuildingIdentity, pf, Accepted, Accepted.AddHours(48), true);
        var record = new MissionAcgBindingRecord(immutable, new MissionAcgInstanceState(MissionAcgLifecycleState.Active, MissionAcgCleanupState.None, Accepted, null), string.Empty);
        Assert.IsTrue(MissionAcgRuntimeMaterializer.TryMaterialize(record, bundle, null, Accepted, out var materialized, out string reason), reason);
        IList<GeneratedMissionObject> objects = materialized.Objects.Select(source => new GeneratedMissionObject
        {
            OwnerId = binding.OwnerId, QuestType = binding.QuestType, QuestInstance = binding.QuestInstance,
            RuntimeType = source.Identity.RuntimeIdentity.Type, RuntimeInstance = source.Identity.RuntimeIdentity.Instance,
            CapturedType = source.Identity.CapturedIdentity.Type, CapturedInstance = source.Identity.CapturedIdentity.Instance,
            Kind = (int)source.Identity.Kind, TemplateId = source.TemplateId,
            X = source.Position.X, Y = source.Position.Y, Z = source.Position.Z,
            HeadingX = source.Heading.X, HeadingY = source.Heading.Y, HeadingZ = source.Heading.Z, HeadingW = source.Heading.W,
            Level = source.Identity.Kind is MissionAcgRuntimeObjectKind.ObjectiveNpc or MissionAcgRuntimeObjectKind.AmbientNpc ? 25 : null,
            CurrentHealth = source.Identity.Kind is MissionAcgRuntimeObjectKind.ObjectiveNpc or MissionAcgRuntimeObjectKind.AmbientNpc ? 401 : null,
            MaxHealth = source.Identity.Kind is MissionAcgRuntimeObjectKind.ObjectiveNpc or MissionAcgRuntimeObjectKind.AmbientNpc ? 625 : null,
            Version = 1, UpdatedAtUtcTicks = Accepted.Ticks
        }).ToList();
        return (binding, materialized, objects);
    }
}
