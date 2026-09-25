namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield.Locality;

    [TestClass]
    public sealed class HitProcTests
    {
        const int ProcNanoId = 204830;
        const int ChancePercent = 80;

        [TestMethod]
        public void OnHitCastChanceLandsNanoOnTheCharacterWhoWasHit()
        {
            var items = new StubItemBuilder().Add(DamageNano(ProcNanoId, damage: 25));
            NpcCharacter attacker = Attacker(items);
            attacker.Stats.Set(CharacterStat.MaxHealth, 100);
            attacker.Stats.Set(CharacterStat.Health, 100);
            Player target = Victim(health: 100);

            Assert.IsTrue(Swing(attacker, target, items, roll: 80));
            Assert.AreEqual(75, target.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(100, attacker.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void OnHitCastChanceMissesWhenTheRollExceedsTheChance()
        {
            var items = new StubItemBuilder().Add(DamageNano(ProcNanoId, damage: 25));
            NpcCharacter attacker = Attacker(items);
            Player target = Victim(health: 100);

            Assert.IsTrue(Swing(attacker, target, items, roll: 81));
            Assert.AreEqual(100, target.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void OnHitCastChanceAimedAtTheUserLandsOnTheAttacker()
        {
            const int healNanoId = 204831;
            var items = new StubItemBuilder().Add(HealNano(healNanoId, heal: 25));
            NpcCharacter attacker = Attacker(items);
            attacker.Stats.Set(CharacterStat.MaxHealth, 100);
            attacker.Stats.Set(CharacterStat.Health, 50);
            Player target = Victim(health: 100);

            Assert.IsTrue(Swing(attacker, target, items, roll: 1, applyOn: ItemTarget.User, nanoId: healNanoId));
            Assert.AreEqual(100, target.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(75, attacker.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void OnHitBuffProcIsAnnouncedToOtherPlayers()
        {
            const int buffId = 204832;
            const int duration = 6000;
            var items = new StubItemBuilder().Add(TestNanos.Create(buffId, durationCentiseconds: duration, ncuCost: 10));
            NpcCharacter attacker = Attacker(items);
            Player target = WatchedPlayer(2, maxNcu: 60);
            Player watcher = WatchedPlayer(4, maxNcu: 60);
            ShareCell(target, watcher);

            Assert.IsTrue(Swing(attacker, target, items, roll: 1, nanoId: buffId));
            Assert.AreEqual(1, target.Buffs.Count);

            CharacterActionMessage announced = DurationMessages(watcher).Single();
            Assert.AreEqual(target.Identity, announced.Identity);
            Assert.AreEqual(IdentityType.NanoProgram, announced.Target.Type);
            Assert.AreEqual(buffId, announced.Target.Instance);
            Assert.AreEqual(attacker.Identity.Instance, announced.Parameter1);
            Assert.AreEqual(duration, announced.Parameter2);
            Assert.AreEqual(1, DurationMessages(target).Count());

            DateTime landedAt = DateTime.UtcNow;
            ((InventoryActionTests.Session)target.Session!).Messages.Clear();
            ((InventoryActionTests.Session)watcher.Session!).Messages.Clear();
            NanoRuntime.Tick(target, landedAt.AddSeconds(61));

            Assert.AreEqual(0, target.Buffs.Count);
            BuffMessage cleared = ((InventoryActionTests.Session)watcher.Session).Messages.OfType<BuffMessage>().Single();
            Assert.AreEqual(target.Identity, cleared.Identity);
            Assert.AreEqual(0, cleared.Action);
            Assert.AreEqual(buffId, cleared.NanoProgram.Instance);
            Assert.AreEqual(1, ((InventoryActionTests.Session)target.Session).Messages.OfType<BuffMessage>().Count());
        }

        static bool Swing(
            NpcCharacter attacker,
            Player target,
            StubItemBuilder items,
            int roll,
            ItemTarget applyOn = ItemTarget.Target,
            int nanoId = ProcNanoId)
        {
            var weapon = new ItemTemplate
            {
                Id = 205012,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnHit] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.CastChance,
                            Target = (int)applyOn,
                            Arguments = [nanoId, ChancePercent]
                        }
                    ]
                }
            };

            return weapon.ExecuteSpells(
                EventType.OnHit,
                target,
                new StubInventoryRepository(),
                items,
                new SpellCriteria { NextRoll = () => roll },
                source: attacker);
        }

        static NpcCharacter Attacker(StubItemBuilder items)
            => new(new Identity { Type = IdentityType.CanbeAffected, Instance = 3 }, items);

        static Player Victim(int health)
        {
            Player player = TestWorld.CreatePlayer(2);
            player.Stats.Set(CharacterStat.MaxHealth, health);
            player.Stats.Set(CharacterStat.Health, health);
            return player;
        }

        static Player WatchedPlayer(int id, int maxNcu)
        {
            Player player = TestWorld.CreatePlayer(id);
            player.Stats.Set(CharacterStat.MaxNCU, maxNcu);
            var session = new InventoryActionTests.Session();
            session.BindPlayer(player);
            player.Session = session;
            return player;
        }

        static void ShareCell(params Dynel[] dynels)
        {
            Cell cell = new CellGrid(null, visibilityNeighborLevel: 0).ResolveCell(default);
            for (int i = 0; i < dynels.Length; i++)
            {
                cell.Add(dynels[i]);
                dynels[i].Cell = cell;
            }
        }

        static IEnumerable<CharacterActionMessage> DurationMessages(Player player)
            => ((InventoryActionTests.Session)player.Session!).Messages
                .OfType<CharacterActionMessage>()
                .Where(message => message.Action == CharacterActionType.SetNanoDuration);

        static NanoSpell DamageNano(int id, int damage)
            => TestNanos.Create(
                id,
                durationCentiseconds: 0,
                can: CanFlags.ApplyOnHostile,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, -damage, -damage]
                    }
                ]);

        static NanoSpell HealNano(int id, int heal)
            => TestNanos.Create(
                id,
                durationCentiseconds: 0,
                can: CanFlags.ApplyOnSelf,
                modifiers:
                [
                    new ItemSpell
                    {
                        FunctionType = (int)FunctionType.Hit,
                        Arguments = [(int)CharacterStat.Health, heal, heal]
                    }
                ]);
    }
}
