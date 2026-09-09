namespace ZoneEngine_New.Tests
{
    using System.Collections.Generic;

    using AORebirth.Enums;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;

    [TestClass]
    public sealed class HitHealthDamageTests
    {
        [TestMethod]
        public void OffensiveHitSendsSignedHealthDamageWithAcDamageType()
        {
            Player target = CreatePlayer(1, out RecordingZoneSession targetSession, health: 200, maxHealth: 200);
            Player caster = CreatePlayer(2, out RecordingZoneSession casterSession, health: 100, maxHealth: 100);

            Assert.IsTrue(
                HitTemplate(
                        (int)CharacterStat.Health,
                        -135,
                        -135,
                        (int)CharacterStat.FireAC)
                    .ExecuteOnUseSpells(
                        target,
                        new StubInventoryRepository(),
                        new StubItemBuilder(),
                        source: caster));

            Assert.AreEqual(65, target.Stats.GetOrZero(CharacterStat.Health));

            HealthDamageMessage message = SingleHealthDamage(targetSession);
            Assert.AreEqual(target.Identity.Instance, message.Identity.Instance);
            Assert.AreEqual(65, message.Unknown1);
            Assert.AreEqual(-135, message.Unknown2);
            Assert.AreEqual((int)CharacterStat.FireAC, message.Unknown3);
            Assert.AreEqual(0, message.Unknown4);
            Assert.AreEqual(caster.Identity.Instance, message.Target.Instance);
            Assert.AreEqual(0, message.Unknown5);
            Assert.AreEqual(1, CountOf<HealthDamageMessage>(casterSession));
        }

        [TestMethod]
        public void CollapsedThreeArgHitUsesThirdValueAsAcType()
        {
            Player target = CreatePlayer(3, out RecordingZoneSession session, health: 100, maxHealth: 100);

            Assert.IsTrue(
                HitTemplate(
                        (int)CharacterStat.Health,
                        -40,
                        (int)CharacterStat.EnergyAC)
                    .ExecuteOnUseSpells(
                        target,
                        new StubInventoryRepository(),
                        new StubItemBuilder()));

            HealthDamageMessage message = SingleHealthDamage(session);
            Assert.AreEqual(60, message.Unknown1);
            Assert.AreEqual(-40, message.Unknown2);
            Assert.AreEqual((int)CharacterStat.EnergyAC, message.Unknown3);
        }

        [TestMethod]
        public void HealHitSendsPositiveAmountWithZeroDamageType()
        {
            Player target = CreatePlayer(4, out RecordingZoneSession session, health: 40, maxHealth: 100);

            Assert.IsTrue(
                HitTemplate((int)CharacterStat.Health, 25, 25, (int)CharacterStat.FireAC)
                    .ExecuteOnUseSpells(
                        target,
                        new StubInventoryRepository(),
                        new StubItemBuilder()));

            Assert.AreEqual(65, target.Stats.GetOrZero(CharacterStat.Health));

            HealthDamageMessage message = SingleHealthDamage(session);
            Assert.AreEqual(65, message.Unknown1);
            Assert.AreEqual(25, message.Unknown2);
            Assert.AreEqual(0, message.Unknown3);
            Assert.AreEqual(0, CountOf<ChatTextMessage>(session));
        }

        static ItemTemplate HitTemplate(params int[] args)
        {
            var arguments = new List<object>(args.Length);
            for (int i = 0; i < args.Length; i++)
                arguments.Add(args[i]);

            return new ItemTemplate
            {
                Id = 9001,
                Name = "Hit Nano",
                Quality = 1,
                SpellList = new Dictionary<EventType, List<ItemSpell>>
                {
                    [EventType.OnUse] =
                    [
                        new ItemSpell
                        {
                            FunctionType = (int)FunctionType.Hit,
                            Target = 2,
                            Arguments = arguments,
                            Requirements = new List<ItemRequirement>()
                        }
                    ]
                }
            };
        }

        static Player CreatePlayer(
            int instance,
            out RecordingZoneSession session,
            int health,
            int maxHealth)
        {
            Player player = TestWorld.CreatePlayer(instance);
            player.Stats.Set(CharacterStat.MaxHealth, maxHealth, StatDetail.Base, dirty: false);
            player.Stats.Set(CharacterStat.Health, health, StatDetail.Base, dirty: false);
            session = new RecordingZoneSession();
            session.BindPlayer(player);
            player.Session = session;
            return player;
        }

        static HealthDamageMessage SingleHealthDamage(RecordingZoneSession session)
        {
            HealthDamageMessage? found = null;
            foreach (MessageBody body in session.Sent)
            {
                if (body is not HealthDamageMessage message)
                    continue;

                Assert.IsNull(found);
                found = message;
            }

            Assert.IsNotNull(found);
            return found!;
        }

        static int CountOf<T>(RecordingZoneSession session)
            where T : MessageBody
        {
            int count = 0;
            foreach (MessageBody body in session.Sent)
            {
                if (body is T)
                    count++;
            }

            return count;
        }
    }
}
