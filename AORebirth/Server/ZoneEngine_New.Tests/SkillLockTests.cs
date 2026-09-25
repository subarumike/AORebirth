namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    [TestClass]
    public sealed class SkillLockTests
    {
        const int WearerApplyOn = 2;
        const int TargetApplyOn = 3;

        [TestMethod]
        public void LockSkillStoresCooldownOnUser()
        {
            Player player = CreatePlayer(1, out _, health: 50);

            Assert.IsTrue(Use(StimTemplate(40), player));

            Assert.AreEqual(80, player.Stats.GetOrZero(CharacterStat.Health));
            TimeSpan remaining = player.SkillLocks.Remaining((int)CharacterStat.FirstAid, DateTime.UtcNow);
            Assert.IsTrue(remaining > TimeSpan.FromSeconds(38) && remaining <= TimeSpan.FromSeconds(40));
            Assert.IsTrue(player.SkillLocks.IsDirty);
        }

        [TestMethod]
        public void LockedSkillRejectsUseBeforeAnyFunctionRuns()
        {
            Player player = CreatePlayer(2, out RecordingZoneSession session, health: 50);
            ItemTemplate stim = StimTemplate(40);
            Assert.IsTrue(Use(stim, player));

            Assert.IsFalse(Use(stim, player));

            Assert.AreEqual(80, player.Stats.GetOrZero(CharacterStat.Health));
            ChatTextMessage text = session.Sent.OfType<ChatTextMessage>().Last();
            StringAssert.StartsWith(text.Text, "Unable to perform action, FirstAid skill is locked, able in 00:00:");
        }

        [TestMethod]
        public void ExpiredLockAllowsUseAgain()
        {
            Player player = CreatePlayer(3, out _, health: 10);
            player.SkillLocks.Lock((int)CharacterStat.FirstAid, 40, DateTime.UtcNow.AddSeconds(-41));

            Assert.IsTrue(Use(StimTemplate(40), player));
            Assert.AreEqual(40, player.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void ZeroDurationClearsLock()
        {
            Player player = CreatePlayer(4, out _, health: 10);
            player.SkillLocks.Lock((int)CharacterStat.FirstAid, 40, DateTime.UtcNow);

            player.SkillLocks.Lock((int)CharacterStat.FirstAid, 0, DateTime.UtcNow);

            Assert.IsFalse(player.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        }

        [TestMethod]
        public void SkillLockModifierShortensDurationAndFloorsAtHalf()
        {
            DateTime now = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
            Player reduced = CreatePlayer(8, out _, health: 10);
            reduced.Stats.Set(CharacterStat.SkillLockModifier, -45, StatDetail.Base, dirty: false);
            reduced.LockSkill((int)CharacterStat.FirstAid, 40, now);
            Assert.AreEqual(TimeSpan.FromSeconds(22), reduced.SkillLocks.Remaining((int)CharacterStat.FirstAid, now));

            Player capped = CreatePlayer(9, out _, health: 10);
            capped.Stats.Set(CharacterStat.SkillLockModifier, -80, StatDetail.Base, dirty: false);
            capped.LockSkill((int)CharacterStat.FirstAid, 40, now);
            Assert.AreEqual(TimeSpan.FromSeconds(20), capped.SkillLocks.Remaining((int)CharacterStat.FirstAid, now));

            Player lengthened = CreatePlayer(10, out _, health: 10);
            lengthened.Stats.Set(CharacterStat.SkillLockModifier, 100, StatDetail.Base, dirty: false);
            lengthened.LockSkill((int)CharacterStat.FirstAid, 40, now);
            Assert.AreEqual(TimeSpan.FromSeconds(80), lengthened.SkillLocks.Remaining((int)CharacterStat.FirstAid, now));
        }

        [TestMethod]
        public void SpecialAttackLocksIgnoreSkillLockModifier()
        {
            DateTime now = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
            CharacterStat[] specials =
            [
                CharacterStat.Brawl,
                CharacterStat.Dimach,
                CharacterStat.SneakAttack,
                CharacterStat.FastAttack,
                CharacterStat.Burst,
                CharacterStat.FlingShot,
                CharacterStat.AimedShot,
                CharacterStat.FullAuto
            ];

            Player player = CreatePlayer(11, out _, health: 10);
            player.Stats.Set(CharacterStat.SkillLockModifier, -50, StatDetail.Base, dirty: false);
            foreach (CharacterStat stat in specials)
                player.LockSkill((int)stat, 40, now);

            foreach (CharacterStat stat in specials)
                Assert.AreEqual(TimeSpan.FromSeconds(40), player.SkillLocks.Remaining((int)stat, now), stat.ToString());
        }

        [TestMethod]
        public void LocksAreKeyedByStat()
        {
            Player player = CreatePlayer(5, out _, health: 10);
            player.SkillLocks.Lock((int)CharacterStat.Burst, 13, DateTime.UtcNow);

            Assert.IsTrue(Use(StimTemplate(40), player));
            Assert.IsTrue(player.SkillLocks.IsLocked((int)CharacterStat.Burst, DateTime.UtcNow));
        }

        [TestMethod]
        public void TargetApplyOnLocksEventTargetNotSource()
        {
            Player user = CreatePlayer(6, out _, health: 10);
            Player patient = CreatePlayer(7, out _, health: 10);

            Assert.IsTrue(StimTemplate(40, TargetApplyOn).ExecuteOnUseSpells(
                patient, new StubInventoryRepository(), new StubItemBuilder(), source: user));

            Assert.IsTrue(patient.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
            Assert.IsFalse(user.SkillLocks.IsLocked((int)CharacterStat.FirstAid, DateTime.UtcNow));
        }

        [TestMethod]
        public void RestoredLockKeepsStoredExpiryAndDropsExpiredRows()
        {
            var locks = new SkillLocks();
            DateTime now = DateTime.UtcNow;

            locks.Restore((int)CharacterStat.FirstAid, now.AddSeconds(25), now);
            locks.Restore((int)CharacterStat.Burst, now.AddSeconds(-1), now);

            Assert.AreEqual(TimeSpan.FromSeconds(25), locks.Remaining((int)CharacterStat.FirstAid, now));
            Assert.IsFalse(locks.IsLocked((int)CharacterStat.Burst, now));
            Assert.IsFalse(locks.IsDirty);
        }

        [TestMethod]
        public void DirtySnapshotHoldsOnlyUnexpiredLocksAndClearsDirty()
        {
            var locks = new SkillLocks();
            DateTime now = DateTime.UtcNow;
            locks.Lock((int)CharacterStat.FirstAid, 40, now);
            locks.Lock((int)CharacterStat.Burst, 5, now.AddSeconds(-10));

            List<SkillLockRecord>? snapshot = locks.TakeDirty(now);

            Assert.IsNotNull(snapshot);
            Assert.AreEqual(1, snapshot!.Count);
            Assert.AreEqual((int)CharacterStat.FirstAid, snapshot[0].StatId);
            Assert.AreEqual(now.AddSeconds(40).Ticks, snapshot[0].ExpiresAtUtcTicks);
            Assert.IsNull(locks.TakeDirty(now));

            locks.RestoreDirty(snapshot);
            Assert.IsTrue(locks.IsDirty);
        }

        static bool Use(ItemTemplate template, Player player)
            => template.ExecuteOnUseSpells(player, new StubInventoryRepository(), new StubItemBuilder());

        /// <summary>Health stim 291043 shape: Hit Health then LockSkill FirstAid.</summary>
        static ItemTemplate StimTemplate(int lockSeconds, int lockApplyOn = WearerApplyOn)
            => new()
            {
                Id = 291043,
                Name = "Health and Nano Stim",
                Quality = 1,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.Hit,
                            Target = TargetApplyOn,
                            Arguments = [(int)CharacterStat.Health, 30, 30, 0]
                        },
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.LockSkill,
                            Target = lockApplyOn,
                            Arguments = [(int)CharacterStat.FirstAid, lockSeconds]
                        }
                    ]
                }
            };

        static Player CreatePlayer(int instance, out RecordingZoneSession session, int health)
        {
            Player player = TestWorld.CreatePlayer(instance);
            player.Stats.Set(CharacterStat.MaxHealth, 100, StatDetail.Base, dirty: false);
            player.Stats.Set(CharacterStat.Health, health, StatDetail.Base, dirty: false);
            session = new RecordingZoneSession();
            session.BindPlayer(player);
            player.Session = session;
            return player;
        }
    }
}
