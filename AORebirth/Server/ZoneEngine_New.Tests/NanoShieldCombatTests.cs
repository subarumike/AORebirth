namespace ZoneEngine_New.Tests;

using System.Linq;

using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Helpers;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class NanoShieldCombatTests
{
    [DataTestMethod]
    [DataRow(70, 200, 42, 28, 77)] // Enough nano to prevent a hit that otherwise kills.
    [DataRow(100, 200, 0, 70, 25)] // The connected hit costs nano only.
    [DataRow(70, 25, 120, 0, 0)] // Limited nano absorption, with an actual killing hit.
    public void MonsterHitReportsPostShieldDamageBeforeDeath(
        int shieldPercent, int nano, int announcedDamage, int remainingHealth, int remainingNano)
    {
        using var world = new TriggerTestWorld();
        var (attacker, target, weapon) = StartFight(world, monsterAttacks: true, shieldPercent, nano);

        Assert.IsTrue(weapon.Tick(weapon.AttackSpeed));

        var hit = world.FirstSession.Sent.OfType<AttackInfoMessage>().Single();
        var codec = new ZoneMessageCodec();
        var decoded = (AttackInfoMessage)codec.Deserialize(codec.Serialize(hit, 127, world.First.Identity.Instance))!.Body;
        Assert.AreEqual(attacker.Identity, decoded.Identity);
        Assert.AreEqual(target.Identity, decoded.Target);
        Assert.AreEqual(announcedDamage, decoded.Unknown1);
        Assert.AreEqual(remainingHealth == 0 ? 4 : 0, decoded.Unknown4);
        Assert.AreEqual(remainingHealth, target.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(remainingNano, target.Stats.GetOrZero(CharacterStat.CurrentNano));
        Assert.AreEqual(remainingHealth == 0, target.IsDead);
        Assert.IsFalse(world.FirstSession.Sent.OfType<HealthDamageMessage>().Any());

        var death = world.FirstSession.Sent.OfType<CharacterActionMessage>()
            .SingleOrDefault(action => action.Identity == target.Identity && action.Action == CharacterActionType.Death);
        if (remainingHealth == 0)
        {
            Assert.IsNotNull(death);
            Assert.IsTrue(world.FirstSession.Sent.IndexOf(hit) < world.FirstSession.Sent.IndexOf(death));
        }
        else
        {
            Assert.IsNull(death);
        }

        target.FlushDirtyStats();
        var stats = world.FirstSession.Sent.OfType<StatMessage>().Where(message => message.Identity == target.Identity)
            .SelectMany(message => message.Stats).ToArray();
        Assert.IsTrue(stats.Any(stat => stat.Value1 == CharacterStat.CurrentNano && stat.Value2 == remainingNano));
        if (announcedDamage > 0)
            Assert.IsTrue(stats.Any(stat => stat.Value1 == CharacterStat.Health && stat.Value2 == remainingHealth));
    }

    [TestMethod]
    public void ReflectAndDamageTypeAbsorbStillPrecedeTheNanoShieldedHit()
    {
        using var world = new TriggerTestWorld();
        var (attacker, target, weapon) = StartFight(world, monsterAttacks: true, shieldPercent: 50, nano: 200);
        target.Stats.Set(CharacterStat.ReflectMeleeAC, 25);
        target.Stats.Set(CharacterStat.MaxReflectedMeleeDmg, 15);
        target.Stats.Set(CharacterStat.AbsorbMeleeAC, 5);

        Assert.IsTrue(weapon.Tick(weapon.AttackSpeed));

        var reflected = world.FirstSession.Sent.OfType<ReflectAttackMessage>().Single();
        var absorbed = world.FirstSession.Sent.OfType<AbsorbMessage>().Single();
        var hit = world.FirstSession.Sent.OfType<AttackInfoMessage>().Single();
        Assert.AreEqual(15, reflected.Unknown1);
        Assert.AreEqual(5, absorbed.Amount);
        Assert.AreEqual(50, hit.Unknown1); // (140 - 35 reflect - 5 typed absorb) / 2.
        Assert.AreEqual(20, target.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(137, target.Stats.GetOrZero(CharacterStat.CurrentNano)); // 50 × 125%, rounded up.
        Assert.AreEqual(985, attacker.Stats.GetOrZero(CharacterStat.Health));
        Assert.IsTrue(world.FirstSession.Sent.IndexOf(reflected) < world.FirstSession.Sent.IndexOf(absorbed));
        Assert.IsTrue(world.FirstSession.Sent.IndexOf(absorbed) < world.FirstSession.Sent.IndexOf(hit));
    }

    [TestMethod]
    public void WeaponSpecialReportsTheSameNanoShieldedDamageItApplies()
    {
        using var world = new TriggerTestWorld();
        var (attacker, target, _) = StartFight(world, monsterAttacks: false, shieldPercent: 70, nano: 200);

        Assert.AreEqual(SpecialAttacks.FireResult.Fired, SpecialAttacks.TryFire(attacker, target, CharacterStat.FastAttack));

        var hit = world.FirstSession.Sent.OfType<SpecialAttackInfo>().Single();
        var codec = new ZoneMessageCodec();
        var decoded = (SpecialAttackInfo)codec.Deserialize(codec.Serialize(hit, 127, world.First.Identity.Instance))!.Body;
        Assert.AreEqual(target.Identity, decoded.Target);
        Assert.AreEqual(42, decoded.Unknown2);
        Assert.AreEqual(0, decoded.Unknown5);
        Assert.AreEqual(28, target.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(77, target.Stats.GetOrZero(CharacterStat.CurrentNano));
        Assert.IsFalse(world.FirstSession.Sent.OfType<HealthDamageMessage>().Any());
    }

    static (Character Attacker, Character Target, CharacterWeapon Weapon) StartFight(
        TriggerTestWorld world, bool monsterAttacks, int shieldPercent, int nano)
    {
        var catalog = new StubCatalog().Add(9001, 1);
        var template = catalog.Require(9001);
        template.Stats[CharacterStat.ItemClass] = (int)ItemClass.Weapon;
        template.Stats[CharacterStat.MinDamage] = 70;
        template.Stats[CharacterStat.MaxDamage] = 70;
        template.Stats[CharacterStat.DamageType] = (int)CharacterStat.MeleeAC;
        template.Stats[CharacterStat.AttackDelay] = 150;
        template.Stats[CharacterStat.RechargeDelay] = 200;
        template.Stats[CharacterStat.AttackRange] = 8;
        template.Stats[CharacterStat.Can] = (int)CanFlags.FastAttack;
        var items = new ItemBuilder(catalog, new StubLogger());
        Item item = items.Create(9001, 9001, 1, ItemSource.Other);
        Player player = world.First;
        var monster = new NpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = 1_000_001 }, items)
        {
            Name = "Nano shield combat fixture",
            Playfield = player.Playfield,
            Position = player.Position
        };
        world.Registry.Register(monster);
        Character attacker = monsterAttacks ? monster : player;
        Character target = monsterAttacks ? player : monster;
        attacker.Stats.Set(CharacterStat.MaxHealth, 1000);
        attacker.Stats.Set(CharacterStat.Health, 1000);
        attacker.Stats.Set(CharacterStat.CriticalIncrease, 0);
        target.Stats.Set(CharacterStat.MaxHealth, 70);
        target.Stats.Set(CharacterStat.Health, 70);
        target.Stats.Set(CharacterStat.MaxNanoEnergy, 200);
        target.Stats.Set(CharacterStat.CurrentNano, nano);
        target.Stats.Set(CharacterStat.AggDef, 75);
        target.Stats.Set(CharacterStat.DamageToNano, shieldPercent);
        target.Stats.Set(CharacterStat.DamageToNanoMultiplier, 125);
        if (monsterAttacks)
            Assert.IsTrue(monster.EquipCombatWeapon(item));
        else
            Assert.IsTrue(player.Inventory.Equipment.Add((int)WeaponSlots.Righthand, item));
        attacker.RebaseWeapons();

        var locality = player.Playfield!.GetRequiredService<PlayfieldLocality>();
        locality.RegisterDynel(monster);
        locality.ActivatePlayerVisibility(player);
        attacker.Stats.Set(CharacterStat.AMSModifier, 400); // This is a guaranteed hit with 140 damage before mitigation.
        attacker.StartFighting(target.Identity, 0);
        Assert.AreEqual(target.Identity, attacker.FightingTarget);
        world.FirstSession.Sent.Clear();
        return (attacker, target, attacker.Weapons.Values.Single());
    }
}
