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

        private static readonly IDictionary<int, int> CapturedStimFiendSawValues =
            new Dictionary<int, int>
            {
                { 10, 54 },
                { 11, 59 },
                { 12, 65 },
                { 13, 70 },
                { 14, 76 }
            };

        private static readonly IDictionary<int, int> CapturedMeldedPatternsSawValues =
            new Dictionary<int, int>
            {
                { 18, 98 },
                { 19, 103 },
                { 20, 109 },
                { 21, 114 },
                { 24, 131 },
                { 25, 136 }
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

        [TestMethod]
        public void StimFiendFormulaReproducesEveryCapturedHeldOutLevelExactly()
        {
            foreach (KeyValuePair<int, int> heldOut in CapturedStimFiendSawValues)
            {
                OrdinaryEnemyCombatNumericSetup setup;
                Assert.IsTrue(TryGenerateStimFiend(heldOut.Key, out setup));
                Assert.AreEqual(
                    OrdinaryEnemyCombatSetupGenerator.StimFiendFormulaId,
                    setup.FormulaId);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown1);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown2);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown3);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown4);
            }

            OrdinaryEnemyCombatNumericSetup generatedLevelSeventeen;
            Assert.IsTrue(TryGenerateStimFiend(17, out generatedLevelSeventeen));
            Assert.AreEqual(
                92,
                generatedLevelSeventeen.SpecialAttackWeaponUnknown1);
        }

        [TestMethod]
        public void StimFiendUsesRuntimeLevelAndRestoresSixOfSevenStartingActors()
        {
            int[] startingQuarantinedSources =
            {
                unchecked((int)0x7953ABAD),
                unchecked((int)0x7953ABBF),
                unchecked((int)0x7953AD68),
                unchecked((int)0x79545069),
                unchecked((int)0x79545072),
                unchecked((int)0x7957E128),
                unchecked((int)0x7957E415)
            };
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                new CapturedTempleOfThreeWindsContentProvider());
            OrdinaryEnemyProfile profile = catalog.GetProfiles().Single(
                value => value.DisplayName == "Stim Fiend"
                         && value.MonsterData
                         == NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData);
            OrdinaryEnemySpawnDefinition[] active =
                catalog.GetSpawns().Where(
                    value => value.PlayfieldInstance == 127
                             && value.ProfileKey == profile.ProfileKey).ToArray();
            Assert.AreEqual(15, active.Length);
            OrdinaryEnemySpawnDefinition[] starting = active.Where(
                value => startingQuarantinedSources.Contains(value.SourceIdentity))
                .ToArray();
            Assert.AreEqual(7, starting.Length);
            CollectionAssert.AreEquivalent(
                new[] { 9, 12, 12, 12, 12, 14, 17 },
                starting.Select(value => value.Level).ToArray());

            int restored = 0;
            int remainedClosed = 0;
            foreach (OrdinaryEnemySpawnDefinition spawn in starting)
            {
                CapturedEnemyCombatContract current =
                    profile.Combat.ResolveContract(spawn.SourceIdentity, spawn.Level);
                CapturedEnemyCombatContract resolved;
                string failure;
                bool success = CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    profile.DisplayName,
                    profile.MonsterData,
                    spawn.Level,
                    spawn.SourceIdentity,
                    current,
                    out resolved,
                    out failure);
                if (spawn.Level == 9)
                {
                    Assert.IsFalse(success);
                    StringAssert.Contains(
                        failure,
                        "no canonical raw combat profile");
                    remainedClosed++;
                    continue;
                }

                Assert.IsTrue(success, failure);
                Assert.IsTrue(current.UsesProductionSpecializedValues);
                Assert.IsTrue(resolved.IsCombatReady);
                Assert.IsTrue(resolved.UsesCaptureProvenArchetype);
                OrdinaryEnemyCombatNumericSetup generated;
                Assert.IsTrue(TryGenerateStimFiend(spawn.Level, out generated));
                Assert.AreEqual(
                    generated.SpecialAttackWeaponUnknown1,
                    resolved.SpecialAttackWeaponUnknown1);
                restored++;
            }

            Assert.AreEqual(6, restored);
            Assert.AreEqual(1, remainedClosed);
        }

        [TestMethod]
        public void StimFiendTerminalOnlyResultDoesNotBecomeAReusableAttackStream()
        {
            CapturedEnemyCombatProfileDefinition levelTwelve =
                CapturedEnemyCombatProfileCatalog.GetProfilesForTests().Single(
                    value => value.ProfileId
                             == "963ecf2aa60f045c-de110ebeb7e358cd");
            Assert.AreEqual(2, levelTwelve.Streams.Length);
            Assert.AreEqual(
                1,
                levelTwelve.Streams.Count(value => value.CapturedTerminalHitOnly));
            CapturedEnemyCombatProfileStreamDefinition reusable =
                levelTwelve.GetReusableNaturalAttackStreams().Single();
            Assert.AreEqual(0, reusable.DamageTypeWire);
            Assert.AreEqual(3, reusable.HitTypeWire);
            Assert.AreEqual(0, reusable.WeaponSlot);
            Assert.AreEqual(
                NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                reusable.WeaponInstance);
            Assert.IsTrue(
                levelTwelve.SupportsCaptureProvenNaturalAttackPacketSemantics);
        }

        [TestMethod]
        public void StimFiendLevelFourteenUsesProductionCadenceWithoutInventingTiming()
        {
            CapturedEnemyCombatProfileDefinition captured =
                CapturedEnemyCombatProfileCatalog.GetProfilesForTests().Single(
                    value => value.ProfileId
                             == "54d40b70fa1a801a-064305180fc7f1ad");
            Assert.IsTrue(captured.CaptureEvidenceSafe);
            Assert.AreEqual(
                0,
                captured.Streams.Single().CapturedLandedIntervalObservationsSeconds.Length);

            CapturedEnemyCombatContract resolved =
                ResolveStimFiend(14, unchecked((int)0x7953ABBF));
            Assert.IsTrue(resolved.UsesProductionSpecializedValues);
            Assert.AreEqual(76, resolved.SpecialAttackWeaponUnknown1);
            Assert.IsTrue(
                resolved.SpecialAttackSequence.RepeatingAttack.RechargeSeconds > 0.0d);
            StringAssert.Contains(
                resolved.CaptureProvenArchetypeId,
                captured.ProfileId);
        }

        [TestMethod]
        public void StimFiendFormulaFailsClosedOutsideItsProvenDomain()
        {
            OrdinaryEnemyCombatNumericSetup ignored;
            Assert.IsFalse(TryGenerateStimFiend(9, out ignored));
            Assert.IsFalse(TryGenerateStimFiend(18, out ignored));
            Assert.IsFalse(
                OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                    Input(
                        NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData,
                        12,
                        NpcCombatAttackRules.CapturedSubwayStimFiendLowTemplate,
                        NpcCombatAttackRules.CapturedSubwayStimFiendHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                        "SIW2"),
                    out ignored));
            Assert.IsFalse(
                OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                    Input(
                        NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData + 1,
                        12,
                        NpcCombatAttackRules.CapturedSubwayStimFiendLowTemplate,
                        NpcCombatAttackRules.CapturedSubwayStimFiendHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                        NpcCombatAttackRules.CapturedSubwayStimFiendWeaponName),
                    out ignored));
        }

        [TestMethod]
        public void CapturedStimFiendSawPacketsRemainExactAndGeneratedLevelSeventeenIsDeterministic()
        {
            AssertStimFiendSawHex(
                10,
                unchecked((int)0x794CD773),
                "1D3C0F1C0000C350794CD77300000007E2000235660002356753495731534957310000003600000036000000360000003600000000");
            AssertStimFiendSawHex(
                11,
                unchecked((int)0x794CD77C),
                "1D3C0F1C0000C350794CD77C00000007E2000235660002356753495731534957310000003B0000003B0000003B0000003B00000000");
            AssertStimFiendSawHex(
                12,
                unchecked((int)0x794CD778),
                "1D3C0F1C0000C350794CD77800000007E2000235660002356753495731534957310000004100000041000000410000004100000000");
            AssertStimFiendSawHex(
                13,
                unchecked((int)0x7953AA4B),
                "1D3C0F1C0000C3507953AA4B00000007E2000235660002356753495731534957310000004600000046000000460000004600000000");
            AssertStimFiendSawHex(
                14,
                unchecked((int)0x7953ABAF),
                "1D3C0F1C0000C3507953ABAF00000007E2000235660002356753495731534957310000004C0000004C0000004C0000004C00000000");

            CapturedEnemyCombatContract generated =
                ResolveStimFiend(17, unchecked((int)0x7953ABAD));
            Assert.AreEqual(92, generated.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(92, generated.SpecialAttackWeaponUnknown4);
            Assert.AreEqual(0, generated.SpecialAttackWeaponUnknown5);
        }

        [TestMethod]
        public void StimFiendSharedPathPreservesSawAttackAttackInfoOrder()
        {
            CapturedEnemyCombatContract contract =
                ResolveStimFiend(17, unchecked((int)0x7953ABAD));
            Identity source = SimpleChar(unchecked((int)0x7953ABAD));
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
                    attack.MinDamage,
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
            Assert.AreEqual(0, attack.AttackInfoWeaponSlot);
            Assert.AreEqual(3, attack.AttackInfoHitType);
            Assert.AreEqual(0, attack.AttackInfoUnknown);
            Assert.AreEqual(
                NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                attack.AttackInfoWeaponInstance);
        }

        [TestMethod]
        public void MeldedPatternsFormulaReproducesEveryCapturedHeldOutLevelExactly()
        {
            foreach (KeyValuePair<int, int> heldOut in CapturedMeldedPatternsSawValues)
            {
                OrdinaryEnemyCombatNumericSetup setup;
                Assert.IsTrue(
                    OrdinaryEnemyCombatSetupGenerator.TryGenerateEquipped(
                        new OrdinaryEnemyEquippedCombatSetupInput(
                            NpcCombatAttackRules.CapturedSubwayMeldedPatternsMonsterData,
                            heldOut.Key,
                            121818,
                            121818,
                            20,
                            NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponSlot),
                        out setup));
                Assert.AreEqual(
                    OrdinaryEnemyCombatSetupGenerator.MeldedPatternsFormulaId,
                    setup.FormulaId);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown1);
                Assert.AreEqual(heldOut.Value + 28, setup.SpecialAttackWeaponUnknown2);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown3);
                Assert.AreEqual(heldOut.Value, setup.SpecialAttackWeaponUnknown4);
            }
        }

        [TestMethod]
        public void EveryCapturedMeldedPatternsSawPacketRemainsByteExactUnderTheFormula()
        {
            CapturedEnemyCombatProfileDefinition[] profiles =
                CapturedEnemyCombatProfileCatalog.GetProfilesForTests().Where(
                    value => value.MonsterData
                             == NpcCombatAttackRules
                                 .CapturedSubwayMeldedPatternsMonsterData)
                    .ToArray();
            Assert.AreEqual(11, profiles.Length);
            foreach (CapturedEnemyCombatProfileDefinition profile in profiles)
            {
                OrdinaryEnemyCombatNumericSetup setup;
                Assert.IsTrue(
                    OrdinaryEnemyCombatSetupGenerator.TryGenerateEquipped(
                        new OrdinaryEnemyEquippedCombatSetupInput(
                            profile.MonsterData,
                            profile.Level,
                            profile.WeaponDefinition.LowId,
                            profile.WeaponDefinition.HighId,
                            profile.WeaponDefinition.Quality,
                            profile.WeaponDefinition.InventorySlot),
                        out setup),
                    profile.ProfileId);
                Assert.AreEqual(
                    profile.SpecialAttackWeaponUnknown1,
                    setup.SpecialAttackWeaponUnknown1,
                    profile.ProfileId);
                Assert.AreEqual(
                    profile.SpecialAttackWeaponUnknown2,
                    setup.SpecialAttackWeaponUnknown2,
                    profile.ProfileId);
                Assert.AreEqual(
                    profile.SpecialAttackWeaponUnknown3,
                    setup.SpecialAttackWeaponUnknown3,
                    profile.ProfileId);
                Assert.AreEqual(
                    profile.SpecialAttackWeaponUnknown4,
                    setup.SpecialAttackWeaponUnknown4,
                    profile.ProfileId);

                CapturedEnemyCombatPacketFixture fixture =
                    CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                        value => value.ProfileId == profile.ProfileId);
                foreach (CapturedEnemySpecialAttackWeaponPacketFixture saw in
                    fixture.SpecialAttackWeaponPackets)
                {
                    MessageBody generated =
                        CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                            SimpleChar(saw.SourceIdentity),
                            profile.SpecialAttacks,
                            profile.SpecialAttackWeaponN3Unknown,
                            setup.SpecialAttackWeaponUnknown1,
                            setup.SpecialAttackWeaponUnknown2,
                            setup.SpecialAttackWeaponUnknown3,
                            setup.SpecialAttackWeaponUnknown4,
                            saw.Unknown5);
                    Assert.AreEqual(
                        saw.BodyHex,
                        BitConverter.ToString(Serialize(generated)).Replace("-", string.Empty),
                        profile.ProfileId);
                }
            }
        }

        [TestMethod]
        public void MeldedPatternsSharedPathPreservesWifuSawAttackAttackInfoOrder()
        {
            CapturedEnemyCombatContract contract =
                ResolveMeldedPatterns(unchecked((int)0x7954508E));
            Identity source = SimpleChar(unchecked((int)0x7954508E));
            Identity target = SimpleChar(unchecked((int)0x7944C065));
            MessageBody[] packets =
            {
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    source,
                    127,
                    new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = contract.WeaponDefinition.InventorySlot
                    },
                    contract.WeaponDefinition),
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    source,
                    contract),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    source,
                    target,
                    contract),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    source,
                    target,
                    1,
                    contract.AttackInfoAmmoCount,
                    contract.AttackInfoWeaponSlot,
                    contract.AttackInfoUnknown,
                    contract.AttackInfoHitType,
                    contract.AttackInfoWeaponInstance,
                    contract.AttackInfoN3Unknown)
            };

            Assert.IsInstanceOfType(packets[0], typeof(WeaponItemFullUpdateMessage));
            Assert.IsInstanceOfType(packets[1], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(packets[2], typeof(AttackMessage));
            Assert.IsInstanceOfType(packets[3], typeof(AttackInfoMessage));
            Assert.AreEqual(125, contract.SpecialAttackWeaponUnknown1);
            Assert.AreEqual(153, contract.SpecialAttackWeaponUnknown2);
            Assert.AreEqual(6, contract.AttackInfoWeaponSlot);
            Assert.AreEqual(0, contract.AttackInfoWeaponInstance);
            Assert.AreEqual(3, contract.AttackInfoHitType);
            Assert.AreEqual(0, contract.AttackInfoUnknown);
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

        private static bool TryGenerateStimFiend(
            int level,
            out OrdinaryEnemyCombatNumericSetup setup)
        {
            return OrdinaryEnemyCombatSetupGenerator.TryGenerate(
                Input(
                    NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData,
                    level,
                    NpcCombatAttackRules.CapturedSubwayStimFiendLowTemplate,
                    NpcCombatAttackRules.CapturedSubwayStimFiendHighTemplate,
                    NpcCombatAttackRules.CapturedSubwayStimFiendWeaponTag,
                    NpcCombatAttackRules.CapturedSubwayStimFiendWeaponName),
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

        private static void AssertStimFiendSawHex(
            int level,
            int sourceInstance,
            string expected)
        {
            CapturedEnemyCombatContract contract =
                ResolveStimFiend(level, sourceInstance);
            MessageBody message =
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    SimpleChar(sourceInstance),
                    contract);
            Assert.AreEqual(
                expected,
                BitConverter.ToString(Serialize(message)).Replace("-", string.Empty));
        }

        private static CapturedEnemyCombatContract ResolveStimFiend(
            int level,
            int sourceInstance)
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                new CapturedTempleOfThreeWindsContentProvider());
            OrdinaryEnemyProfile profile = catalog.GetProfiles().Single(
                value => value.DisplayName == "Stim Fiend"
                         && value.MonsterData
                         == NpcCombatAttackRules.CapturedSubwayStimFiendMonsterData);
            CapturedEnemyCombatContract current =
                profile.Combat.ResolveContract(sourceInstance, level);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    profile.DisplayName,
                    profile.MonsterData,
                    level,
                    sourceInstance,
                    current,
                    out resolved,
                    out failure),
                failure);
            return resolved;
        }

        private static CapturedEnemyCombatContract ResolveMeldedPatterns(
            int sourceInstance)
        {
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                new CapturedTempleOfThreeWindsContentProvider());
            OrdinaryEnemyProfile profile = catalog.GetProfiles().Single(
                value => value.DisplayName == "Melded Patterns"
                         && value.MonsterData
                            == NpcCombatAttackRules
                                .CapturedSubwayMeldedPatternsMonsterData);
            OrdinaryEnemySpawnDefinition spawn = catalog.GetSpawns().Single(
                value => value.PlayfieldInstance == 127
                         && value.ProfileKey == profile.ProfileKey
                         && value.SourceIdentity == sourceInstance);
            OrdinaryEnemySpawnVariant variant =
                spawn.LevelDefinition.GetExplicitVariants().Single();
            CapturedEnemyCombatContract current =
                profile.Combat.ResolveContract(sourceInstance, variant);
            current.Retaliates = true;
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    profile.DisplayName,
                    profile.MonsterData,
                    variant.Level,
                    sourceInstance,
                    current,
                    out resolved,
                    out failure),
                failure);
            return resolved;
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
