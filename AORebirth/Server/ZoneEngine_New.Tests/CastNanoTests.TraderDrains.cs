namespace ZoneEngine_New.Tests
{
    using System;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Playfield;

    public sealed partial class CastNanoTests
    {
        [DataTestMethod]
        [DataRow(266914, 133, 100)]
        [DataRow(266919, 307, 271)]
        public void TraderDrainRetainsInitialRecipientsThroughAllSixApplications(int nanoId, int drain, int restore)
        {
            var items = new StubItemBuilder().Add(TraderDrain(nanoId, drain, restore));
            using var world = new FanoutWorld(items);
            Player trader = world.Player(10, 0, 0, 0);
            NpcCharacter victim = world.Npc(11, 1, 0, 0, attackable: true);
            NpcCharacter other = world.Npc(12, 2, 0, 0, attackable: true);
            SetNano(trader, 0);
            SetNano(victim, 2000);
            SetNano(other, 2000);
            DateTime now = DateTime.UtcNow;
            trader.SetFightingTarget(victim.Identity);

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(trader, victim, nanoId, items, new SilentInventory(), now));
            Assert.AreEqual(trader.Identity, RequireBuff(victim, nanoId).Source);
            Assert.AreEqual(2000 - drain, Nano(victim));
            Assert.AreEqual(restore, Nano(trader));

            trader.SetFightingTarget(other.Identity);
            NanoRuntime.Tick(victim, now.AddMilliseconds(4999));
            Assert.AreEqual(2000 - drain, Nano(victim));
            for (int i = 1; i <= 6; i++)
                NanoRuntime.Tick(victim, now.AddSeconds(5 * i));

            Assert.AreEqual(2000 - 6 * drain, Nano(victim));
            Assert.AreEqual(6 * restore, Nano(trader));
            Assert.AreEqual(2000, Nano(other));
            Assert.IsFalse(victim.TryGetBuff(nanoId, out _));
        }

        [TestMethod]
        public void TraderDrainKeepsVictimAndSkipsRestoreWhileCasterIsUnavailable()
        {
            const int nanoId = 266914;
            var items = new StubItemBuilder().Add(TraderDrain(nanoId, drain: 133, restore: 100));
            using var world = new FanoutWorld(items);
            DynelRegistry registry = world.Playfield.GetRequiredService<DynelRegistry>();
            Player trader = world.Player(20, 0, 0, 0);
            NpcCharacter victim = world.Npc(21, 1, 0, 0, attackable: true);
            SetNano(trader, 0);
            SetNano(victim, 1000);
            trader.SetFightingTarget(victim.Identity);
            DateTime now = DateTime.UtcNow;

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(trader, victim, nanoId, items, new SilentInventory(), now));
            Assert.IsTrue(registry.UnregisterExact(trader));
            NanoRuntime.Tick(victim, now.AddSeconds(5));
            Assert.AreEqual(734, Nano(victim));
            Assert.AreEqual(100, Nano(trader));

            registry.Register(trader);
            trader.SetFightingTarget(Identity.None);
            NanoRuntime.Tick(victim, now.AddSeconds(10));
            Assert.AreEqual(601, Nano(victim));
            Assert.AreEqual(200, Nano(trader));
        }

        [TestMethod]
        public void TraderDrainDoesNotRestoreNanoToTheDeadCasterOrTheVictim()
        {
            const int nanoId = 266919;
            var items = new StubItemBuilder().Add(TraderDrain(nanoId, drain: 307, restore: 271));
            using var world = new FanoutWorld(items, new StubGameData(HashItemCatalog.Parse("{}")) { AllowMissingXpLevels = true });
            Player trader = world.Player(25, 0, 0, 0);
            NpcCharacter victim = world.Npc(26, 1, 0, 0, attackable: true);
            SetNano(trader, 0);
            SetNano(victim, 2000);
            DateTime now = DateTime.UtcNow;

            Assert.IsTrue(NanoRuntime.TryApplyImmediate(trader, victim, nanoId, items, new SilentInventory(), now));
            trader.OnDeath();
            Assert.IsTrue(trader.IsDead);
            int nanoAtDeath = Nano(trader);
            NanoRuntime.Tick(victim, now.AddSeconds(5));

            Assert.AreEqual(1386, Nano(victim));
            Assert.AreEqual(nanoAtDeath, Nano(trader));
        }

        static NanoSpell TraderDrain(int nanoId, int drain, int restore)
            => TestNanos.Create(nanoId, durationCentiseconds: 3000, can: 0, flags: NanoFlags.IsHostile | NanoFlags.IsBuff,
                modifiers: [TimedNanoHit(ItemTarget.Fightingtarget, -drain), TimedNanoHit(ItemTarget.User, restore)]);

        static ItemSpell TimedNanoHit(ItemTarget recipient, int delta)
            => new()
            {
                FunctionType = (int)FunctionType.Hit,
                Target = (int)recipient,
                Arguments = [(int)CharacterStat.CurrentNano, delta, delta, 0],
                TickCount = 6,
                TickInterval = 500
            };

        static void SetNano(Character character, int nano)
        {
            character.Stats.Set(CharacterStat.MaxNanoEnergy, 2000);
            character.Stats.Set(CharacterStat.CurrentNano, nano);
        }

        static int Nano(Character character) => character.Stats.GetOrZero(CharacterStat.CurrentNano);
    }
}
