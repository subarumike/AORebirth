namespace ZoneEngine_New.Tests;

using System;
using System.Linq;

using AORebirth.Enums;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Characters;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.MessageHandlers;
using ZoneEngine_New.Core.Nanos;
using ZoneEngine_New.Core.Network;

[TestClass]
public sealed class SkillTrainingTests
{
    [TestMethod]
    public void GameData_catalog_loads_every_trainable_breed_and_profession()
    {
        SkillCatalog catalog = SkillCatalog.Default;
        foreach (Breed breed in new[] { Breed.Solitus, Breed.Opifex, Breed.Nanomage, Breed.Atrox })
            Assert.IsTrue(catalog.Trains(breed, Profession.Soldier), breed.ToString());
        foreach (Profession profession in System.Enum.GetValues<Profession>())
            Assert.AreEqual(profession is not (Profession.None or Profession.Monster), catalog.Trains(Breed.Solitus, profession), profession.ToString());
    }

    [TestMethod]
    public void One_packet_charges_the_whole_raise_once_from_stored_ip()
    {
        Player player = SolitusSoldier();

        Assert.IsTrue(SkillTraining.TryTrain(player, Pairs((CharacterStat.Strength, 7), (CharacterStat.MartialArts, 6)), SkillCatalog.Default));

        Assert.AreEqual(1478, player.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base));
        Assert.AreEqual(7, player.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Base));
        Assert.AreEqual(6, player.Stats.GetOrZero(CharacterStat.MartialArts, StatDetail.Base));
        Assert.AreEqual(7, player.Stats.GetOrZero(CharacterStat.MartialArts), "floor((0.2*7 + 0.5*6 + 0.3*6) / 4) = 1 trickle");

        Assert.IsTrue(SkillTraining.TryTrain(player, Pairs((CharacterStat.MartialArts, 7)), SkillCatalog.Default));
        Assert.AreEqual(1466, player.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base), "prices base 6, not the trickled 7");
    }

    [TestMethod]
    public void Illegal_packets_change_nothing()
    {
        (CharacterStat, uint)[][] rejected =
        [
            [(CharacterStat.Strength, 5)],
            [(CharacterStat.MartialArts, 4)],
            [(CharacterStat.MartialArts, 6), (CharacterStat.MartialArts, 7)],
            [(CharacterStat.MartialArts, 6), (CharacterStat.GmLevel, 1)],
            [(CharacterStat.IP, 999999)],
            [(CharacterStat.Cash, 1000000)],
            [(CharacterStat.MartialArts, SkillTraining.MaxBase + 1)],
            [(CharacterStat.MartialArts, uint.MaxValue)],
            [(CharacterStat.Pistol, 60)],
            [(CharacterStat.MartialArts, 5)],
            [(CharacterStat.Strength, 7), (CharacterStat.Pistol, 5)],
        ];

        foreach ((CharacterStat, uint)[] pairs in rejected)
        {
            Player player = SolitusSoldier();
            player.Stats.Set(CharacterStat.Strength, 7, StatDetail.Base);
            Assert.IsFalse(SkillTraining.TryTrain(player, Pairs(pairs), SkillCatalog.Default), string.Join(",", pairs));
            AssertUntouched(player, strength: 7);
        }
    }

    [TestMethod]
    public void Untrainable_breed_or_profession_changes_nothing()
    {
        foreach ((CharacterStat stat, int value) in new[] { (CharacterStat.Breed, 0), (CharacterStat.Breed, 5), (CharacterStat.Profession, 0), (CharacterStat.Profession, (int)Profession.Monster) })
        {
            Player player = SolitusSoldier();
            player.Stats.Set(stat, value, StatDetail.Base);
            Assert.IsFalse(SkillTraining.TryTrain(player, Pairs((CharacterStat.MartialArts, 6)), SkillCatalog.Default));
            AssertUntouched(player, strength: 6);
        }
    }

    [TestMethod]
    public void Skill_cost_uses_the_profession_column_and_breed_multiplier()
    {
        Player soldier = SolitusSoldier();
        Assert.IsTrue(SkillTraining.TryTrain(soldier, Pairs((CharacterStat.Pistol, 8)), SkillCatalog.Default));
        Assert.AreEqual(1500 - (5 + 6 + 7), soldier.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base));

        Player atrox = SolitusSoldier();
        atrox.Stats.Set(CharacterStat.Breed, (int)Breed.Atrox, StatDetail.Base);
        atrox.Stats.Set(CharacterStat.Strength, 15, StatDetail.Base);
        Assert.IsTrue(SkillTraining.TryTrain(atrox, Pairs((CharacterStat.Strength, 17)), SkillCatalog.Default));
        Assert.AreEqual(1500 - (15 + 16), atrox.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base));
    }

    [TestMethod]
    public void Level_caps_ramp_within_each_title_and_step_past_level_200()
    {
        SkillCatalog catalog = SkillCatalog.Default;
        int Cap(Breed breed, Profession profession, CharacterStat stat, int level) => catalog.LevelCap(breed, profession, stat, level);

        (int Level, int Cap)[] pistol = [(1, 10), (11, 60), (14, 60), (15, 65), (49, 200), (50, 205), (200, 600), (220, 1100)];
        foreach ((int level, int cap) in pistol)
            Assert.AreEqual(cap, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.Pistol, level), "1.0 Pistol at level " + level);

        Assert.AreEqual(9, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.FullAuto, 1), "1.5 ramps 4 per level");
        Assert.AreEqual(540, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.FullAuto, 200));
        Assert.AreEqual(560, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.FullAuto, 201));
        Assert.AreEqual(480, Cap(Breed.Solitus, Profession.Engineer, CharacterStat.Shotgun, 200), "3.2 tier");
        Assert.AreEqual(495, Cap(Breed.Solitus, Profession.Engineer, CharacterStat.Shotgun, 201));
        Assert.AreEqual(520, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.Dimach, 210), "4.0 tier steps 10");
        Assert.AreEqual(460, Cap(Breed.Solitus, Profession.Nanotechnician, CharacterStat.FullAuto, 220), "5.0 tier steps 5");

        Assert.AreEqual(9, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.Strength, 1), "breed base + 3 per level");
        Assert.AreEqual(306, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.Strength, 100));
        Assert.AreEqual(472, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.Strength, 200));
        Assert.AreEqual(544, Cap(Breed.Opifex, Profession.Soldier, CharacterStat.Agility, 200));
        Assert.AreEqual(712, Cap(Breed.Atrox, Profession.Soldier, CharacterStat.Strength, 210));

        Assert.AreEqual(0, Cap(Breed.Solitus, Profession.Soldier, CharacterStat.GmLevel, 200));
    }

    [TestMethod]
    public void Raises_past_the_level_cap_are_rejected()
    {
        Player player = SolitusSoldier();
        Assert.IsFalse(SkillTraining.TryTrain(player, Pairs((CharacterStat.Pistol, 11)), SkillCatalog.Default));
        Assert.IsFalse(SkillTraining.TryTrain(player, Pairs((CharacterStat.Strength, 10)), SkillCatalog.Default));
        AssertUntouched(player, strength: 6);

        Assert.IsTrue(SkillTraining.TryTrain(player, Pairs((CharacterStat.Pistol, 10), (CharacterStat.Strength, 9)), SkillCatalog.Default));
        Assert.AreEqual(1500 - (5 + 6 + 7 + 8 + 9) - 2 * (6 + 7 + 8), player.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base));

        Player overCap = SolitusSoldier();
        overCap.Stats.Set(CharacterStat.Pistol, 20, StatDetail.Base);
        Assert.IsTrue(SkillTraining.TryTrain(overCap, Pairs((CharacterStat.Pistol, 20), (CharacterStat.MartialArts, 6)), SkillCatalog.Default));
        Assert.IsFalse(SkillTraining.TryTrain(overCap, Pairs((CharacterStat.Pistol, 21)), SkillCatalog.Default));
    }

    [TestMethod]
    public void Skill_raises_are_capped_by_abilities_including_the_same_packet()
    {
        Player Level30() { Player player = SolitusSoldier(); player.Stats.Set(CharacterStat.Level, 30, StatDetail.Base); return player; }

        Assert.AreEqual(12, SkillCatalog.Default.AbilityCap(Level30().Stats, Profession.Soldier, CharacterStat.Pistol, 30), "floor(2 * 6)");
        Assert.IsTrue(SkillTraining.TryTrain(Level30(), Pairs((CharacterStat.Pistol, 12)), SkillCatalog.Default));
        Assert.IsFalse(SkillTraining.TryTrain(Level30(), Pairs((CharacterStat.Pistol, 13)), SkillCatalog.Default));

        Assert.IsTrue(SkillTraining.TryTrain(Level30(), Pairs((CharacterStat.Pistol, 16), (CharacterStat.Agility, 10)), SkillCatalog.Default), "floor(2 * (0.6 * 10 + 0.4 * 6)) = floor(16.8)");
        Assert.IsFalse(SkillTraining.TryTrain(Level30(), Pairs((CharacterStat.Pistol, 17), (CharacterStat.Agility, 10)), SkillCatalog.Default));
    }

    [TestMethod]
    public void Past_level_200_the_ability_cap_grows_with_the_skill_tier_step()
    {
        Player player = SolitusSoldier();
        player.Stats.Set(CharacterStat.Level, 220, StatDetail.Base);
        player.Stats.Set(CharacterStat.Stamina, 125, StatDetail.Base);
        player.Stats.Set(CharacterStat.BodyDevelopment, 250, StatDetail.Base);
        player.Stats.Set(CharacterStat.IP, 1000000, StatDetail.Base);

        Assert.AreEqual(250 + 20 * 25, SkillCatalog.Default.AbilityCap(player.Stats, Profession.Soldier, CharacterStat.BodyDevelopment, 220), "1.1 tier steps 25");
        Assert.AreEqual(250, SkillCatalog.Default.AbilityCap(player.Stats, Profession.Soldier, CharacterStat.BodyDevelopment, 200));
        Assert.IsTrue(SkillTraining.TryTrain(player, Pairs((CharacterStat.BodyDevelopment, 560)), SkillCatalog.Default, out string? rejection), rejection);
    }

    [TestMethod]
    public void Multipliers_up_to_3_4_share_the_2_5_tier_step_past_level_200()
    {
        SkillCatalog catalog = SkillCatalog.Default;
        Assert.AreEqual(
            catalog.LevelCap(Breed.Solitus, Profession.Engineer, CharacterStat.Shotgun, 210),
            catalog.LevelCap(Breed.Solitus, Profession.Shade, CharacterStat.PhysicalInit, 210),
            "3.2 and 3.4 both step 15");
    }

    [TestMethod]
    public void Rejections_report_the_reason_with_cost_and_ip()
    {
        Player player = SolitusSoldier();
        player.Stats.Set(CharacterStat.IP, 10, StatDetail.Base);

        Assert.IsFalse(SkillTraining.TryTrain(player, Pairs((CharacterStat.Pistol, 8)), SkillCatalog.Default, out string? rejection));
        Assert.AreEqual("cost 18 exceeds ip 10 (short 8)", rejection);
        Assert.AreEqual("Pistol 5->8 cost 18", SkillTraining.DescribeRequest(player, Pairs((CharacterStat.Pistol, 8)), SkillCatalog.Default));

        Assert.IsFalse(SkillTraining.TryTrain(player, Pairs((CharacterStat.Pistol, 11)), SkillCatalog.Default, out rejection));
        Assert.AreEqual("Pistol 5->11 over level cap 10 at level 1", rejection);

        Assert.IsTrue(SkillTraining.TryTrain(player, Pairs((CharacterStat.Pistol, 6)), SkillCatalog.Default, out rejection));
        Assert.IsNull(rejection);
    }

    [TestMethod]
    public void Absurd_ability_values_saturate_instead_of_throwing()
    {
        Player player = SolitusSoldier();
        player.Stats.Set(CharacterStat.Agility, int.MaxValue, StatDetail.Base);

        Assert.AreEqual(int.MaxValue, SkillCatalog.Default.AbilityCap(player.Stats, Profession.Soldier, CharacterStat.Pistol, 1));
        Assert.AreEqual((int)((60L * int.MaxValue + 40 * 6) / 400), SkillCatalog.Default.Trickle(player.Stats, CharacterStat.Pistol));
    }

    [TestMethod]
    public void Declared_skill_count_larger_than_the_packet_is_rejected_before_allocation()
    {
        var codec = new ZoneMessageCodec();
        byte[] packet = codec.Serialize(new SkillMessage { Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 7101 }, Skills = Pairs((CharacterStat.Pistol, 7)) }, 7101, 2);
        Assert.AreEqual((CharacterStat.Pistol, 7u), Values((SkillMessage)codec.Deserialize(packet)!.Body).Single());

        byte[] countThenPair = [0, 0, 0, 1, 0, 0, 0, (byte)CharacterStat.Pistol, 0, 0, 0, 7];
        int count = Enumerable.Range(0, packet.Length - countThenPair.Length + 1)
            .Single(i => packet.AsSpan(i, countThenPair.Length).SequenceEqual(countThenPair));
        foreach (int declared in new[] { int.MaxValue, 1000, -1 })
        {
            byte[] forged = (byte[])packet.Clone();
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(forged.AsSpan(count), declared);
            Exception thrown = Assert.ThrowsExactly<Exception>(() => codec.Deserialize(forged), declared.ToString());
            Assert.IsInstanceOfType<System.IO.InvalidDataException>(thrown.GetBaseException(), declared.ToString());
        }
    }

    [TestMethod]
    public void Training_body_development_raises_max_health_and_marks_it_for_the_client()
    {
        Player player = SolitusSoldier();
        player.RebaseStats();
        int before = player.Stats.GetOrZero(CharacterStat.MaxHealth);
        player.Stats.DrainDirty();

        Assert.IsTrue(SkillTraining.TryTrain(player, Pairs((CharacterStat.BodyDevelopment, 8)), SkillCatalog.Default, out string? rejection), rejection);

        Assert.AreEqual(before + 3 * 3, player.Stats.GetOrZero(CharacterStat.MaxHealth), "Solitus gains 3 health per Body Development point");
        CollectionAssert.Contains(player.Stats.DrainDirty().Select(stat => stat.Value1).ToArray(), CharacterStat.MaxHealth);
    }

    [TestMethod]
    public void Body_development_trickle_reaches_max_health_once()
    {
        Player player = SolitusSoldier();
        player.Stats.Set(CharacterStat.Stamina, 10, StatDetail.Base);

        player.RebaseStats();
        player.RebaseStats();

        Assert.AreEqual(5, player.Stats.GetOrZero(CharacterStat.BodyDevelopment, StatDetail.Base));
        Assert.AreEqual(7, player.Stats.GetOrZero(CharacterStat.BodyDevelopment));
        Assert.AreEqual(10 + 6 + 7 * 3, player.Stats.GetOrZero(CharacterStat.MaxHealth));
    }

    [TestMethod]
    public void Rebase_keeps_full_vitals_at_the_bonused_max_so_regen_stays_idle()
    {
        Player player = SolitusSoldier();
        player.Stats.Set(CharacterStat.MaxNCU, 60, StatDetail.Base);
        NanoSpell buff = TestNanos.Create(1020, modifiers:
        [
            new ItemSpell { FunctionType = (int)FunctionType.Modify, Arguments = [(int)CharacterStat.MaxHealth, 50] },
            new ItemSpell { FunctionType = (int)FunctionType.Modify, Arguments = [(int)CharacterStat.MaxNanoEnergy, 40] },
        ]);
        Assert.AreEqual(BuffApplyDecision.Apply,
            player.TryApplyBuff(buff, player.Identity, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), out _, out _));
        player.RebaseStats();
        int maxHealth = player.Stats.GetOrZero(CharacterStat.MaxHealth);
        int maxNano = player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy);
        Assert.AreEqual(50, maxHealth - player.Stats.GetOrZero(CharacterStat.MaxHealth, StatDetail.Base));
        player.Stats.Set(CharacterStat.Health, maxHealth, StatDetail.Base);
        player.Stats.Set(CharacterStat.CurrentNano, maxNano, StatDetail.Base);

        player.RebaseStats();

        Assert.AreEqual(maxHealth, player.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(maxNano, player.Stats.GetOrZero(CharacterStat.CurrentNano));
    }

    [TestMethod]
    public void Handler_trains_only_the_session_character_and_replies_with_bases_and_ip()
    {
        Player player = SolitusSoldier();
        var session = new RecordingZoneSession { State = SessionState.InPlay };
        session.BindPlayer(player);
        player.Session = session;
        var handler = new SkillMessageHandler();

        handler.Handle(new SkillMessage
        {
            Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = player.Identity.Instance + 1 },
            Skills = Pairs((CharacterStat.MartialArts, 6)),
        }, session);
        Assert.AreEqual(0, session.Sent.Count);
        Assert.AreEqual(5, player.Stats.GetOrZero(CharacterStat.MartialArts, StatDetail.Base));

        handler.Handle(new SkillMessage { Identity = player.Identity, Skills = Pairs((CharacterStat.MartialArts, 6), (CharacterStat.GmLevel, 1)) }, session);
        var rejected = (SkillMessage)session.Sent.Single();
        Assert.AreEqual(0, rejected.Unknown);
        CollectionAssert.AreEqual(new[] { (CharacterStat.MartialArts, 5u), (CharacterStat.IP, 1500u) }, Values(rejected));

        session.Sent.Clear();
        handler.Handle(new SkillMessage { Identity = player.Identity, Skills = Pairs((CharacterStat.MartialArts, 6)) }, session);
        var accepted = (SkillMessage)session.Sent.Single();
        CollectionAssert.AreEqual(new[] { (CharacterStat.MartialArts, 6u), (CharacterStat.IP, 1490u) }, Values(accepted));
    }

    static Player SolitusSoldier()
    {
        Player player = TestWorld.CreatePlayer(7101);
        player.Stats.Set(CharacterStat.Breed, (int)Breed.Solitus, StatDetail.Base);
        player.Stats.Set(CharacterStat.Profession, (int)Profession.Soldier, StatDetail.Base);
        player.Stats.Set(CharacterStat.Level, 1, StatDetail.Base);
        player.Stats.Set(CharacterStat.TitleLevel, 1, StatDetail.Base);
        player.Stats.Set(CharacterStat.IP, 1500, StatDetail.Base);
        for (CharacterStat ability = CharacterStat.Strength; ability <= CharacterStat.Psychic; ability++)
            player.Stats.Set(ability, 6, StatDetail.Base);
        for (CharacterStat skill = CharacterStat.MartialArts; skill <= CharacterStat.NanoResist; skill++)
            player.Stats.Set(skill, SkillCatalog.SkillFloor, StatDetail.Base);
        return player;
    }

    static void AssertUntouched(Player player, int strength)
    {
        Assert.AreEqual(1500, player.Stats.GetOrZero(CharacterStat.IP, StatDetail.Base));
        Assert.AreEqual(strength, player.Stats.GetOrZero(CharacterStat.Strength, StatDetail.Base));
        Assert.AreEqual(5, player.Stats.GetOrZero(CharacterStat.MartialArts, StatDetail.Base));
        Assert.AreEqual(5, player.Stats.GetOrZero(CharacterStat.Pistol, StatDetail.Base));
        Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.GmLevel, StatDetail.Base));
        Assert.AreEqual(0, player.Stats.GetOrZero(CharacterStat.Cash, StatDetail.Base));
    }

    static GameTuple<CharacterStat, uint>[] Pairs(params (CharacterStat Stat, uint Value)[] pairs)
        => pairs.Select(pair => new GameTuple<CharacterStat, uint> { Value1 = pair.Stat, Value2 = pair.Value }).ToArray();

    static (CharacterStat, uint)[] Values(SkillMessage message)
        => message.Skills.Select(pair => (pair.Value1, pair.Value2)).ToArray();
}
