namespace ZoneEngine_New.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Ai;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Mobs;
    using ZoneEngine_New.Core.Nanos;

    public sealed partial class CastNanoTests
    {
        [TestMethod]
        public void EncounterNanoNeedsStrictHealthThresholdAndWaitsForAuthoredCooldown()
        {
            using var encounter = new TriggerEncounter();
            DateTime now = DateTime.UtcNow.AddMinutes(1);
            encounter.EnterFightAtHealth(50);
            encounter.Caster.Tick(now);
            Assert.AreEqual(100, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));

            encounter.EnterFightAtHealth(49);
            DateTime firstCast = now.AddSeconds(3);
            encounter.Caster.Tick(firstCast);
            Assert.AreEqual(50, encounter.Npc.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(95, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.IsTrue(encounter.Npc.IsInNanoRecharge(firstCast));

            encounter.EnterFightAtHealth(49);
            encounter.Caster.Tick(firstCast.AddSeconds(91));
            Assert.AreEqual(95, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));
            encounter.Caster.Tick(firstCast.AddSeconds(94));
            Assert.AreEqual(90, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));
            Assert.AreEqual(50, encounter.Npc.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void EncounterNanoKeepsTriggerThroughRechargeAndStunInterruption()
        {
            using var encounter = new TriggerEncounter(attackDelay: 400, maxCastsPerFight: 1);
            DateTime now = DateTime.UtcNow.AddMinutes(1);
            encounter.EnterFightAtHealth(49);
            encounter.Npc.StartNanoRecharge(200, now);
            encounter.Caster.Tick(now);
            Assert.IsNull(encounter.Npc.PendingCast);
            Assert.AreEqual(100, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));

            DateTime firstStart = now.AddSeconds(3);
            encounter.Caster.Tick(firstStart);
            Assert.IsNotNull(encounter.Npc.PendingCast);
            Assert.AreEqual(95, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));

            NanoSpell stun = TestNanos.Create(ChildId, durationCentiseconds: 3000,
                modifiers: [new ItemSpell { FunctionType = (int)FunctionType.Stun }]);
            encounter.Npc.TryApplyBuff(stun, encounter.Npc.Identity, firstStart.AddSeconds(1), out Buff? applied, out _);
            Assert.IsNotNull(applied);
            Assert.IsTrue(encounter.Npc.IsStunned);
            Assert.IsNull(encounter.Npc.PendingCast);
            encounter.Caster.Tick(firstStart.AddSeconds(20));
            Assert.AreEqual(95, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));

            DateTime retry = firstStart.AddSeconds(35);
            NanoRuntime.Tick(encounter.Npc, retry);
            Assert.IsFalse(encounter.Npc.IsStunned);
            encounter.Caster.Tick(retry);
            Assert.IsNotNull(encounter.Npc.PendingCast);
            NanoRuntime.Tick(encounter.Npc, retry.AddSeconds(4));
            Assert.AreEqual(50, encounter.Npc.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(90, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));

            encounter.EnterFightAtHealth(49);
            encounter.Caster.Tick(retry.AddMinutes(2));
            Assert.IsNull(encounter.Npc.PendingCast);
            Assert.AreEqual(90, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void EncounterNanoCannotLandAfterResetOrDeath(bool dies)
        {
            using var encounter = new TriggerEncounter(attackDelay: 400);
            DateTime now = DateTime.UtcNow.AddMinutes(1);
            encounter.EnterFightAtHealth(49);
            encounter.Caster.Tick(now);
            Assert.IsNotNull(encounter.Npc.PendingCast);

            if (dies)
            {
                encounter.Npc.OnDeath();
            }
            else
            {
                encounter.Npc.OnReset();
            }
            Assert.IsNull(encounter.Npc.PendingCast);
            NanoRuntime.Tick(encounter.Npc, now.AddSeconds(5));
            Assert.IsFalse(encounter.Npc.IsInNanoRecharge(now.AddSeconds(5)));
            Assert.AreEqual(95, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));

            if (dies)
                return;

            encounter.EnterFightAtHealth(49);
            encounter.Caster.Tick(now.AddSeconds(30));
            Assert.IsNotNull(encounter.Npc.PendingCast);
            NanoRuntime.Tick(encounter.Npc, now.AddSeconds(34));
            Assert.AreEqual(50, encounter.Npc.Stats.GetOrZero(CharacterStat.Health));
        }

        [TestMethod]
        public void EncounterNanoRulesLeaveOtherAutonomousNpcNanosAvailable()
        {
            using var encounter = new TriggerEncounter(includeOrdinaryHeal: true);
            encounter.Npc.Stats.Set(CharacterStat.Health, 49);
            encounter.Caster.Tick(DateTime.UtcNow.AddMinutes(1));

            Assert.AreEqual(61, encounter.Npc.Stats.GetOrZero(CharacterStat.Health));
            Assert.AreEqual(100, encounter.Npc.Stats.GetOrZero(CharacterStat.CurrentNano));
        }

        sealed class TriggerEncounter : IDisposable
        {
            const string SpawnHash = "TEST";
            readonly DirectoryInfo _content = Directory.CreateTempSubdirectory("ao-trigger-nano-");
            readonly FanoutWorld _world;
            readonly Player _opponent;

            public NpcCharacter Npc { get; }
            public NpcNanoCaster Caster { get; }

            public TriggerEncounter(int attackDelay = 0, int? maxCastsPerFight = null, bool includeOrdinaryHeal = false)
            {
                NanoSpell triggered = TestNanos.Create(BuffId, durationCentiseconds: 0, nanoPointCost: 5,
                    attackDelay: attackDelay, attackDelayCap: attackDelay, rechargeDelay: 200,
                    modifiers: [new ItemSpell { FunctionType = (int)FunctionType.Hit, Arguments = [(int)CharacterStat.Health, 1, 1] }]);
                var items = new StubItemBuilder().Add(triggered);
                if (includeOrdinaryHeal)
                {
                    items.Add(InstantHit(InstantId, heal: 12));
                }

                var rules = new Dictionary<string, MonsterCombatRules>
                {
                    [SpawnHash] = new MonsterCombatRules
                    {
                        TriggeredSelfNanos = [new MonsterTriggeredSelfNano
                        {
                            NanoId = triggered.Id,
                            HealthBelowPercent = 50,
                            ChancePercent = 100,
                            MaxCastsPerFight = maxCastsPerFight,
                            CooldownSeconds = 94
                        }]
                    }
                };
                File.WriteAllText(Path.Combine(_content.FullName, MonsterCombatRulesCatalog.FileName), JsonSerializer.Serialize(rules));
                _world = new FanoutWorld(items, new StubGameData(HashItemCatalog.Parse(null), rootPath: _content.FullName));
                _opponent = _world.Player(1, 0, 0, 0);
                Npc = _world.Npc(2, 0, 0, 0, attackable: true, spawnHash: SpawnHash);
                Npc.Stats.Set(CharacterStat.MaxHealth, 100);
                Npc.Stats.Set(CharacterStat.Health, 100);
                Npc.Stats.Set(CharacterStat.CurrentNano, 100);
                Npc.TryAddUploadedNano(triggered.Id);
                if (includeOrdinaryHeal)
                {
                    Npc.TryAddUploadedNano(InstantId);
                }
                NpcBrain brain = NpcBrain.Create(Npc);
                Caster = (NpcNanoCaster)typeof(NpcBrain).GetField("_nanos", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(brain)!;
            }

            public void EnterFightAtHealth(int health)
            {
                Npc.SetFightingTarget(_opponent.Identity);
                Npc.Stats.Set(CharacterStat.Health, health);
            }

            public void Dispose()
            {
                _world.Dispose();
                File.Delete(Path.Combine(_content.FullName, MonsterCombatRulesCatalog.FileName));
                _content.Delete();
            }
        }
    }
}
