namespace ZoneEngine_New.Tests;

using System.Linq;
using AORebirth.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Playfield.Locality;

[TestClass]
public sealed class MissionNpcCombatPolicyTests
{
    [TestMethod]
    public void MissionActorCombatCapabilityUsesItsNativeEnabledState()
    {
        var actor = new GeneratedMissionNpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = 1_000_001 },
            new StubItemBuilder(), null!);
        var target = new Identity { Type = IdentityType.CanbeAffected, Instance = 42 };
        Assert.IsFalse(actor.AcceptsPlayerCombatNanos);
        actor.StartFighting(target, 0);
        Assert.AreEqual(Identity.None, actor.FightingTarget);
        actor.CombatEnabled = true;
        actor.Stats.Set(CharacterStat.Health, 100);
        actor.StartFighting(target, 0);
        Assert.IsTrue(actor.AcceptsPlayerCombatNanos);
        Assert.AreEqual(target, actor.FightingTarget);
        actor.CombatEnabled = false;
        Assert.IsFalse(actor.AcceptsPlayerCombatNanos);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PlayerAndMissionNpcUseNativeTemplateWeaponSwings(bool npcAttacks)
    {
        using var world = new AcceptedSubwayShopRuntimeTests.World();
        var catalog = new StubCatalog().AddWeapon(9001, 1);
        var template = catalog.Require(9001);
        template.Stats[CharacterStat.MinDamage] = 7;
        template.Stats[CharacterStat.MaxDamage] = 7;
        template.Stats[CharacterStat.DamageType] = (int)CharacterStat.MeleeAC;
        template.Stats[CharacterStat.AttackDelay] = 150;
        template.Stats[CharacterStat.RechargeDelay] = 200;
        template.Stats[CharacterStat.AttackRange] = 8;
        var items = new ItemBuilder(catalog, new StubLogger());
        var item = items.Create(9001, 9001, 1, ItemSource.Other);
        NpcCharacter npc = npcAttacks
            ? new GeneratedMissionNpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = 1_000_001 }, items, null!) { CombatEnabled = true }
            : new NpcCharacter(new() { Type = IdentityType.CanbeAffected, Instance = 1_000_001 }, items);
        npc.Name = "Native combat fixture";
        npc.Playfield = world.Player.Playfield;
        npc.Position = world.Player.Position;
        world.Registry.Register(npc);
        foreach (Character actor in new Character[] { world.Player, npc })
        {
            actor.Stats.Set(CharacterStat.MaxHealth, 10000);
            actor.Stats.Set(CharacterStat.Health, 10000);
            actor.Stats.Set(CharacterStat.AggDef, 75);
            actor.Stats.Set(CharacterStat.CriticalIncrease, 0);
        }
        Character attacker = npcAttacks ? npc : world.Player;
        Character target = npcAttacks ? world.Player : npc;
        if (npcAttacks) npc.Equipment.Add(npc.Equipment.Offset, item);
        else world.Player.Inventory.Equipment.Add((int)WeaponSlots.Righthand, item);
        attacker.RebaseWeapons();
        var weapon = attacker.Weapons.Values.Single();
        Assert.AreSame(item, weapon.Item);
        Assert.AreEqual(1.5, weapon.AttackSpeed);
        Assert.AreEqual(2.0, weapon.RechargeSpeed);
        Assert.AreEqual(8.0, weapon.GetAttackRange());

        var locality = world.Player.Playfield!.GetRequiredService<PlayfieldLocality>();
        locality.RegisterDynel(world.Player);
        locality.RegisterDynel(npc);
        locality.ActivatePlayerVisibility(world.Player);
        world.Session.Messages.Clear();
        attacker.Stats.Set(CharacterStat.AMSModifier, 400); // Native hit probability exceeds one; no seeded RNG.
        attacker.StartFighting(target.Identity, 0);
        Assert.AreEqual(target.Identity, attacker.FightingTarget);
        Assert.AreEqual(1, world.Session.Messages.OfType<SpecialAttackWeaponMessage>().Count());
        var started = world.Session.Messages.OfType<AttackMessage>().Single();
        Assert.AreEqual(attacker.Identity, started.Identity);
        Assert.AreEqual(target.Identity, started.Target);
        world.Session.Messages.Clear();
        Assert.IsTrue(weapon.Tick(weapon.AttackSpeed)); // Invokes the native ProcessWeaponSwing subscriber.
        var hit = world.Session.Messages.OfType<AttackInfoMessage>().Single();
        Assert.AreEqual(14, hit.Unknown1); // Fixed template damage7 scaled by native AMS400.
        Assert.AreEqual(9986, target.Stats.GetOrZero(CharacterStat.Health));
        Assert.AreEqual(0, world.Session.Messages.OfType<MissedAttackInfoMessage>().Count());

        attacker.Stats.Set(CharacterStat.AMSModifier, 0);
        target.Stats.Set(CharacterStat.DMSModifier, 100000);
        // Native hit chance has a positive floor. The chance of all32 hits here is below 1e-29.
        for (int attempt = 0; attempt < 32; attempt++)
        {
            int before = target.Stats.GetOrZero(CharacterStat.Health);
            world.Session.Messages.Clear();
            weapon.ResetAttack();
            Assert.IsTrue(weapon.Tick(weapon.AttackSpeed));
            if (world.Session.Messages.OfType<MissedAttackInfoMessage>().Any())
            {
                var miss = world.Session.Messages.OfType<MissedAttackInfoMessage>().Single();
                Assert.AreEqual(attacker.Identity, miss.Identity);
                Assert.AreEqual(target.Identity, miss.Unknown4);
                Assert.AreEqual(before, target.Stats.GetOrZero(CharacterStat.Health));
                Assert.AreEqual(0, world.Session.Messages.OfType<AttackInfoMessage>().Count());
                return;
            }
            Assert.AreEqual(7, world.Session.Messages.OfType<AttackInfoMessage>().Single().Unknown1);
            Assert.AreEqual(before - 7, target.Stats.GetOrZero(CharacterStat.Health));
        }
        Assert.Fail("No native miss occurred in the bounded high-defense sample.");
    }
}
