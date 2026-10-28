namespace ZoneEngine_New.Tests;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using AORebirth.Core.GameData;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Quests.Dungeons;

[TestClass]
[DoNotParallelize]
public sealed class MissionTrapResourceTests
{
    static readonly Lazy<ItemTemplateCatalog> Templates = new(() =>
    {
        string? root = Environment.GetEnvironmentVariable(GameDataPaths.EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(root))
            Assert.Inconclusive("Set AO_REBIRTH_GAMEDATA_PATH to the existing canonical GameData for recovered trap resource checks.");
        Assert.IsTrue(File.Exists(Path.Combine(root!, "items.dat")), "The configured canonical items.dat is missing.");
        var data = new StubGameData(HashItemCatalog.Parse("{}")) { RootPath = root! };
        return new ItemTemplateCatalog(data, new StubLogger());
    });

    [TestMethod]
    [Timeout(30000)]
    public void DisarmToolAuthoredStatsDoNotBecomeAnInventedSkillRequirement()
    {
        // Recovery002/003: this is an item stat, not an original server skill threshold.
        ItemTemplate tool = Templates.Value.Require(95576);
        Assert.AreEqual("Bomb Disarmament Tools", tool.Name);
        Assert.AreEqual(51005, tool.DynelType);
        Assert.AreEqual(0x80009, tool.Stats[CharacterStat.Can]);
        Assert.AreEqual(10, tool.Stats[CharacterStat.BreakingEntry]);
        // Original item resource calls stat294 ItemDelay; the current enum calls it AttackDelay.
        Assert.AreEqual(10, tool.Stats[CharacterStat.AttackDelay]);
        Assert.AreEqual(0, tool.Actions.Count);
        Assert.AreEqual(0, tool.SpellList.Values.Sum(spells => spells.Count));
        Assert.AreEqual(0, tool.Stats[CharacterStat.Can] & 0x20,
            "The unmodified authored tool does not select the recovered local quantity branch.");
    }

    [DataTestMethod]
    [DataRow(152936, 20, 83957)]
    [DataRow(152937, 800, 83955)]
    [Timeout(30000)]
    public void SmokeBombEndpointsKeepTheirAuthoredDifficultyAndNanoProgram(int templateId, int difficulty, int nanoId)
    {
        ItemTemplate trap = Templates.Value.Require(templateId);
        Assert.AreEqual("A Smoke Bomb", trap.Name);
        Assert.AreEqual(51008, trap.DynelType);
        Assert.AreEqual(difficulty, trap.Stats[CharacterStat.TrapDifficulty]);
        Assert.AreEqual(1, trap.Stats[CharacterStat.AttackCount]);
        ItemSpell cast = trap.SpellList[EventType.OnUse].Single(spell => spell.Is(FunctionType.CastNano));
        Assert.IsTrue(cast.TryReadInt(0, out int authoredNano));
        Assert.AreEqual(nanoId, authoredNano);
        // The endpoint observations do not establish a server nano-selection threshold or interpolation formula.
    }

    [DataTestMethod]
    [DataRow(83957, -88, 149, 1400)]
    [DataRow(83955, -118, 193, 1600)]
    [Timeout(30000)]
    public void SmokeNanoProgramsPreserveSignedAmsDebuffsAndTauntWithoutInventingDamage(
        int templateId, int modifier, int tauntMagnitude, int rawTimeExist)
    {
        ItemTemplate template = Templates.Value.Require(templateId);
        Assert.AreEqual(rawTimeExist, template.Stats[CharacterStat.TimeExist]);
        NanoSpell nano = NanoSpell.From(template);
        Assert.IsTrue(NanoRuntime.SupportsEnvironmentalPassiveBuff(template),
            "The recovered resource must be admitted by the environmental passive-buff path.");
        ItemSpell[] programs = template.SpellList[EventType.OnUse].ToArray();
        CollectionAssert.AreEquivalent(new[] { (int)FunctionType.Skill, (int)FunctionType.TauntNpc },
            programs.Select(spell => spell.FunctionType).ToArray());
        ItemSpell skill = programs.Single(spell => spell.Is(FunctionType.Skill));
        Assert.IsTrue(skill.TryReadInt(0, out int stat));
        Assert.IsTrue(skill.TryReadInt(1, out int amount));
        Assert.AreEqual((int)CharacterStat.AMSModifier, stat);
        Assert.AreEqual(modifier, amount);
        ItemSpell taunt = programs.Single(spell => spell.Is(FunctionType.TauntNpc));
        Assert.IsTrue(taunt.TryReadInt(0, out int magnitude));
        Assert.AreEqual(tauntMagnitude, magnitude);
        Assert.IsFalse(programs.Any(spell => spell.Is(FunctionType.Hit) || spell.Is(FunctionType.AreaHit)
            || spell.Is(FunctionType.DrainHit) || spell.Is(FunctionType.SpecialHit)));
        Assert.AreEqual(rawTimeExist, nano.DurationCentiseconds,
            "Use the existing nano duration interpretation; this check does not assign new units to captured operands.");
    }

    [TestMethod]
    [TestCategory("MissionTrapPrivateEvidence")]
    [Timeout(30000)]
    public void RetainedResourceAllocationsStillMatchTheRecoveredOriginalBytes()
    {
        using JsonDocument evidence = ReadPrivate("research-consolidation.json");
        JsonElement records = evidence.RootElement.GetProperty("OriginalResourceRecords");
        Assert.AreEqual(6, records.GetArrayLength());
        foreach (JsonElement resource in records.EnumerateArray())
        {
            JsonElement source = resource.GetProperty("source");
            byte[] bytes = Convert.FromHexString(resource.GetProperty("allocation_hex").GetString()!);
            Assert.AreEqual(source.GetProperty("allocation_length").GetInt32(), bytes.Length);
            Assert.AreEqual(source.GetProperty("allocation_sha256").GetString(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            Assert.AreEqual(bytes.Length, resource.GetProperty("parsed_end_offset").GetInt32(),
                "Only fully framed selected original trap/nano resources are used by this fixture.");
        }
        Assert.AreEqual(13, evidence.RootElement.GetProperty("ExistingEvent109Count").GetInt32());
        Assert.AreEqual(4, evidence.RootElement.GetProperty("ExistingSmokeBombSequenceCount").GetInt32());
    }

    [TestMethod]
    [TestCategory("MissionTrapPrivateEvidence")]
    [Timeout(30000)]
    public void ThirteenRetainedTriggerEventsKeepExactTrapActorAndParentIdentities()
    {
        using JsonDocument fixtures = ReadPrivate("historical-trap-fixtures.json");
        JsonElement root = fixtures.RootElement;
        JsonElement joins = root.GetProperty("Event109Joins");
        Assert.AreEqual(13, joins.GetArrayLength());
        foreach (JsonElement join in joins.EnumerateArray())
        {
            string capture = join.GetProperty("capture").GetString()!;
            byte[] action = Packet(root, capture, join.GetProperty("event_ordinal").GetInt32());
            Assert.AreEqual(45, action.Length);
            Assert.AreEqual(0x2049527Cu, Word(action, 16));
            AssertIdentity(action, 20, join.GetProperty("trap_identity"));
            Assert.AreEqual(1u, Word(action, 29));
            Assert.AreEqual(109u, Word(action, 33));
            AssertIdentity(action, 37, join.GetProperty("actor_identity"));
            byte[] trap = Packet(root, capture, join.GetProperty("trap_full_update_ordinal").GetInt32());
            Assert.AreEqual(0x59313928u, Word(trap, 16));
            AssertIdentity(trap, 20, join.GetProperty("trap_identity"));
            Assert.AreEqual(11u, Word(trap, 29));
            AssertIdentity(trap, 33, join.GetProperty("parent_identity"));
            Assert.IsTrue(Word(trap, 33) is 51016u or 51017u,
                "The retained controls are attached to exact doors/chests, not invented parentless traps.");
        }
    }

    [TestMethod]
    [TestCategory("MissionTrapPrivateEvidence")]
    [Timeout(30000)]
    public void FourRetainedSmokeSequencesKeepCastAndApplicationSourceDomainsDistinct()
    {
        using JsonDocument fixtures = ReadPrivate("historical-trap-fixtures.json");
        JsonElement root = fixtures.RootElement;
        JsonElement sequences = root.GetProperty("SmokeBombSequences");
        Assert.AreEqual(4, sequences.GetArrayLength());
        foreach (JsonElement sequence in sequences.EnumerateArray())
        {
            string capture = sequence.GetProperty("capture").GetString()!;
            uint nanoId = sequence.GetProperty("nano_resource_identity")[1].GetUInt32();
            uint trapInstance = sequence.GetProperty("trap_identity")[1].GetUInt32();
            int castOrdinal = sequence.GetProperty("cast_packet").GetProperty("ordinal").GetInt32();
            int triggerOrdinal = sequence.GetProperty("trigger_event_packet").GetProperty("ordinal").GetInt32();
            int acknowledgementOrdinal = sequence.GetProperty("character_action107_packets")[0].GetProperty("ordinal").GetInt32();
            int applyOrdinal = sequence.GetProperty("running_apply_packet").GetProperty("ordinal").GetInt32();
            Assert.IsTrue(castOrdinal < triggerOrdinal && triggerOrdinal < acknowledgementOrdinal && acknowledgementOrdinal < applyOrdinal);

            byte[] cast = Packet(root, capture, castOrdinal);
            Assert.AreEqual(0x25314D6Du, Word(cast, 16));
            AssertIdentity(cast, 20, sequence.GetProperty("actor_identity"));
            Assert.AreEqual(nanoId, Word(cast, 29));
            AssertIdentity(cast, 33, sequence.GetProperty("actor_identity"));
            AssertIdentity(cast, 45, sequence.GetProperty("trap_identity"));

            byte[] acknowledgement = Packet(root, capture, acknowledgementOrdinal);
            Assert.AreEqual(0x5E477770u, Word(acknowledgement, 16));
            AssertIdentity(acknowledgement, 20, sequence.GetProperty("actor_identity"));
            Assert.AreEqual(107u, Word(acknowledgement, 29));
            Assert.AreEqual(0u, Word(acknowledgement, 33));
            Assert.AreEqual(0u, Word(acknowledgement, 37));
            Assert.AreEqual(0u, Word(acknowledgement, 41));
            Assert.AreEqual(1u, Word(acknowledgement, 45));
            Assert.AreEqual(nanoId, Word(acknowledgement, 49));

            byte[] application = Packet(root, capture, applyOrdinal);
            Assert.AreEqual(0x5E477770u, Word(application, 16));
            AssertIdentity(application, 20, sequence.GetProperty("actor_identity"));
            Assert.AreEqual(98u, Word(application, 29));
            Assert.AreEqual(53019u, Word(application, 37));
            Assert.AreEqual(nanoId, Word(application, 41));
            Assert.AreEqual(trapInstance, Word(application, 45));
            Assert.AreEqual(sequence.GetProperty("duration_operand").GetUInt32(), Word(application, 49));
            Assert.AreEqual(51008u, Word(cast, 45));
            Assert.AreEqual(50000u, sequence.GetProperty("native_constructed_apply_context")[0].GetUInt32(),
                "The native CA98 context uses another identity type despite sharing the raw trap instance.");
        }
    }

    [TestMethod]
    [TestCategory("MissionTrapPrivateEvidence")]
    [Timeout(30000)]
    public void PrivateDevelopmentProfileRemainsDisabledAndPreservesUninterpretedCapturedFields()
    {
        using JsonDocument fragment = ReadPrivate("DungeonGeneration.traps.fragment.json");
        MissionTrapPolicy policy = JsonSerializer.Deserialize<MissionTrapPolicy>(
            fragment.RootElement.GetProperty("Traps").GetRawText())!;
        Assert.IsTrue(policy.IsValid);
        Assert.IsFalse(policy.Enabled, "The unverified new-client neutral binding is not enabled by a fixture check.");
        Assert.AreEqual("Disabled", policy.Interaction.DetectionMode);
        Assert.AreEqual("Disabled", policy.Interaction.DisarmMode);
        MissionTrapSpawnDefinition spawn = policy.Profiles.Single().Spawn;
        using JsonDocument fixtures = ReadPrivate("historical-trap-fixtures.json");
        JsonElement sequence = fixtures.RootElement.GetProperty("SmokeBombSequences")[0];
        byte[] captured = Packet(fixtures.RootElement, sequence.GetProperty("capture").GetString()!,
            sequence.GetProperty("trap_update").GetProperty("ordinal").GetInt32());
        Assert.AreEqual(Word(captured, 29), (uint)spawn.CommonMarker);
        Assert.AreEqual(Word(captured, 45), (uint)spawn.NeutralType);
        Assert.AreEqual(Word(captured, 49), (uint)spawn.NeutralInstance);
        Assert.AreEqual(BinaryPrimitives.ReadInt16BigEndian(captured.AsSpan(53, 2)), spawn.HeaderStatBytes);
        int encodedCount = checked((int)Word(captured, 55));
        int statCount = encodedCount / 1009 - 1;
        Assert.AreEqual(statCount, spawn.Stats.Length);
        for (int index = 0; index < statCount; index++)
        {
            Assert.AreEqual(Word(captured, 59 + index * 8), (uint)spawn.Stats[index].StatId);
            Assert.AreEqual(Word(captured, 63 + index * 8), spawn.Stats[index].Value);
        }
        int blobOffset = 59 + statCount * 8;
        Assert.AreEqual(0u, Word(captured, blobOffset));
        Assert.AreEqual(0, spawn.OpaqueData.Length);
        int tail = blobOffset + 4;
        Assert.AreEqual(Word(captured, tail), (uint)spawn.TrapMarker);
        Assert.AreEqual(Word(captured, tail + 4), (uint)spawn.Raw74);
        Assert.AreEqual(Word(captured, tail + 8), (uint)spawn.Identity78Type);
        Assert.AreEqual(Word(captured, tail + 12), (uint)spawn.Identity78Instance);
        Assert.AreEqual(Word(captured, tail + 16), (uint)spawn.Identity80Type);
        Assert.AreEqual(Word(captured, tail + 20), (uint)spawn.Identity80Instance);
        Assert.AreEqual(captured[tail + 24], spawn.TrapFlags);
    }

    static JsonDocument ReadPrivate(string file)
    {
        string? root = Environment.GetEnvironmentVariable("AO_REBIRTH_MISSION_TRAP_EVIDENCE_ROOT");
        if (string.IsNullOrWhiteSpace(root))
            Assert.Inconclusive("SKIP: provide the existing retained trap evidence using AO_REBIRTH_MISSION_TRAP_EVIDENCE_ROOT; no private bytes are bundled in public tests.");
        string path = Path.Combine(root!, file);
        Assert.IsTrue(File.Exists(path), "Configured private trap evidence file is missing: " + file);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    static byte[] Packet(JsonElement root, string capture, int ordinal)
    {
        JsonElement packet = root.GetProperty("Packets").EnumerateArray().Single(candidate =>
            candidate.GetProperty("capture").GetString() == capture && candidate.GetProperty("ordinal").GetInt32() == ordinal);
        byte[] bytes = Convert.FromHexString(packet.GetProperty("raw_hex").GetString()!);
        Assert.AreEqual(packet.GetProperty("packet_length").GetInt32(), bytes.Length);
        Assert.AreEqual(packet.GetProperty("packet_sha256").GetString(),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return bytes;
    }

    static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));

    static void AssertIdentity(byte[] bytes, int offset, JsonElement identity)
    {
        Assert.AreEqual(identity[0].GetUInt32(), Word(bytes, offset));
        Assert.AreEqual(identity[1].GetUInt32(), Word(bytes, offset + 4));
    }
}
