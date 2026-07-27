namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using AORebirth.Core.Playfields;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using ZoneEngine.Core.Playfields;

    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    [TestClass]
    public class OrdinaryEnemyCombatSetupGeneratorTests
    {
        private static readonly IDictionary<int, int> CapturedSawValues =
            new Dictionary<int, int>
            {
                { 5, 30 },
                { 6, 35 },
                { 8, 45 },
                { 9, 49 },
                { 10, 54 }
            };

        [TestMethod]
        public void DisobedientBotFormulaReproducesEveryCapturedHeldOutLevelExactly()
        {
            foreach (KeyValuePair<int, int> heldOut in CapturedSawValues)
            {
                OrdinaryEnemyCombatNumericSetup setup;
                Assert.IsTrue(
                    TryGenerate(heldOut.Key, out setup),
                    "Held-out level " + heldOut.Key + " must remain inside the proven domain.");
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown1);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown2);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown3);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown4);
            }
        }

        [TestMethod]
        public void DisobedientBotFormulaUsesPopulationLevelWithoutActorIdentity()
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                new CapturedTempleOfThreeWindsContentProvider());
            OrdinaryEnemyProfile profile = catalog.GetProfiles().Single(
                value => value.DisplayName == "Disobedient Bot"
                         && value.MonsterData
                         == NpcCombatAttackRules
                             .CapturedSubwayDisobedientBotMonsterData);
            OrdinaryEnemySpawnDefinition[] bots =
                catalog.GetSpawns()
                    .Where(
                        value => value.PlayfieldInstance == 127
                                 && value.ProfileKey == profile.ProfileKey)
                    .ToArray();
            Assert.AreEqual(12, bots.Length);

            foreach (OrdinaryEnemySpawnDefinition bot in bots)
            {
                OrdinaryEnemyCombatNumericSetup generated;
                Assert.IsTrue(TryGenerate(bot.Level, out generated));
                CapturedEnemyCombatContract contract =
                    profile.Combat.ResolveContract(bot.SourceIdentity, bot.Level);
                if (!contract.UsesProductionSpecializedValues)
                {
                    Assert.Fail(contract.Evidence);
                }
                Assert.AreEqual(
                    generated.SpecialAttackWeaponUnknown1,
                    contract.SpecialAttackWeaponUnknown1,
                    string.Format("Source 0x{0:X8}", bot.SourceIdentity));
            }
        }

        [TestMethod]
        public void DisobedientBotGeneratedLevelsResolveTheCaptureProvenArchetype()
        {
            foreach (int level in new[] { 6, 7, 8, 9, 10 })
            {
                CapturedEnemyCombatContract current =
                    CapturedSubwayCombatCatalog.For(
                        "Disobedient Bot",
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData,
                        level);
                if (current.AttackModel != CapturedEnemyAttackModel.Specialized)
                {
                    Assert.Fail(current.Evidence);
                }
                CapturedEnemyCombatContract resolved;
                string failure;
                Assert.IsTrue(
                    CapturedEnemyCombatProfileCatalog.TryResolve(
                        127,
                        "Disobedient Bot",
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData,
                        level,
                        0,
                        current,
                        out resolved,
                        out failure),
                    failure);
                Assert.IsTrue(resolved.IsCombatReady);
                Assert.AreEqual(current.SpecialAttackWeaponUnknown1, resolved.SpecialAttackWeaponUnknown1);
                CapturedEnemyCombatAttackDefinition attack =
                    resolved.SpecialAttackSequence.RepeatingAttack;
                Assert.AreEqual(NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponSlot, attack.AttackInfoWeaponSlot);
                Assert.AreEqual(NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag, attack.AttackInfoWeaponInstance);
                Assert.AreEqual(NpcCombatAttackRules.NormalAttackInfoHitType, attack.AttackInfoHitType);
                Assert.AreEqual(0, attack.AttackInfoUnknown);
            }
        }

        [TestMethod]
        public void DisobedientBotFormulaFailsClosedOutsideItsCategoricalDomain()
        {
            OrdinaryEnemyCombatNumericSetup ignored;
            Assert.IsFalse(TryGenerate(4, out ignored));
            Assert.IsFalse(TryGenerate(11, out ignored));
            Assert.IsFalse(
                OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                    Input(
                        17649,
                        8,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate + 1,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponName),
                    out ignored));
            Assert.IsFalse(
                OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                    Input(
                        17649,
                        8,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                        "SIW2"),
                    out ignored));
        }

        [TestMethod]
        public void CapturedDisobedientBotSawPacketsRemainExactAndLevelSevenIsDeterministic()
        {
            AssertSawHex(
                5,
                unchecked((int)0x794E807A),
                "1D3C0F1C0000C350794E807A00000007E2000235660002356753495731534957310000001E0000001E0000001E0000001E00000000");
            AssertSawHex(
                6,
                unchecked((int)0x794F6080),
                "1D3C0F1C0000C350794F608000000007E2000235660002356753495731534957310000002300000023000000230000002300000000");
            AssertSawHex(
                8,
                unchecked((int)0x794DF074),
                "1D3C0F1C0000C350794DF07400000007E2000235660002356753495731534957310000002D0000002D0000002D0000002D00000000");
            AssertSawHex(
                9,
                unchecked((int)0x7953AD69),
                "1D3C0F1C0000C3507953AD6900000007E2000235660002356753495731534957310000003100000031000000310000003100000000");
            AssertSawHex(
                10,
                unchecked((int)0x7953AA81),
                "1D3C0F1C0000C3507953AA8100000007E2000235660002356753495731534957310000003600000036000000360000003600000000");

            CapturedEnemyCombatContract levelSeven =
                CapturedSubwayCombatCatalog.For("Disobedient Bot", 17649, 7);
            Assert.AreEqual(40, levelSeven.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(40, levelSeven.SpecialAttackWeaponUnknown4);
        }

        [TestMethod]
        public void DisobedientBotSharedPathPreservesSawAttackAttackInfoOrder()
        {
            CapturedEnemyCombatContract contract =
                Resolve(8, unchecked((int)0x794DF074));
            Identity source = SimpleChar(unchecked((int)0x794DF074));
            Identity target = SimpleChar(unchecked((int)0x7944C065));
            CapturedEnemyCombatAttackDefinition attack =
                contract.SpecialAttackSequence.RepeatingAttack;
            MessageBody[] packets =
            {
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(source, contract),
                CapturedEnemyCombatPacketFactory.CreateAttack(source, target, contract),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    source,
                    target,
                    14,
                    attack.AttackInfoAmmoCount,
                    attack.AttackInfoWeaponSlot,
                    attack.AttackInfoUnknown,
                    attack.AttackInfoHitType,
                    attack.AttackInfoWeaponInstance,
                    attack.AttackInfoN3Unknown)
            };

            Assert.IsInstanceOfType(packets[0], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(packets[1], typeof(AttackMessage));
            Assert.IsInstanceOfType(packets[2], typeof(AttackInfoMessage));
        }

        private static OrdinaryEnemyCombatSetupInput Input(
            int monsterData,
            int level,
            int lowTemplate,
            int highTemplate,
            int tag,
            string name)
        {
            return new OrdinaryEnemyCombatSetupInput(
                monsterData,
                level,
                lowTemplate,
                highTemplate,
                tag,
                name);
        }

        private static bool TryGenerate(
            int level,
            out OrdinaryEnemyCombatNumericSetup setup)
        {
            return OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                Input(
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData,
                    level,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotLowTemplate,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotHighTemplate,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponTag,
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotWeaponName),
                out setup);
        }

        private static void AssertSawHex(int level, int sourceInstance, string expected)
        {
            CapturedEnemyCombatContract contract =
                Resolve(level, sourceInstance);
            MessageBody message =
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    SimpleChar(sourceInstance),
                    contract);
            Assert.AreEqual(expected, BitConverter.ToString(Serialize(message)).Replace("-", string.Empty));
        }

        private static Identity SimpleChar(int instance)
        {
            return new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
        }

        private static CapturedEnemyCombatContract Resolve(
            int level,
            int sourceInstance)
        {
            CapturedEnemyCombatContract current =
                CapturedSubwayCombatCatalog.For(
                    "Disobedient Bot",
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData,
                    level);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Disobedient Bot",
                    NpcCombatAttackRules.CapturedSubwayDisobedientBotMonsterData,
                    level,
                    sourceInstance,
                    current,
                    out resolved,
                    out failure),
                failure);
            return resolved;
        }

        private static byte[] Serialize(MessageBody body)
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(body.GetType());
            using (var memoryStream = new MemoryStream())
            using (var writer = new StreamWriter(memoryStream))
            {
                serializer.Serialize(writer, new SerializationContext(resolver), body);
                return memoryStream.ToArray();
            }
        }
    }
}
