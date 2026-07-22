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
    public class CapturedEnemyCombatPacketFactoryTests
    {
        private const int ThiefSourceIdentity = unchecked((int)0x795B5DB2);

        private const int CultistSourceIdentity = unchecked((int)0x7984B379);

        private const int LocalPlayerIdentity = unchecked((int)0x70CBBEF3);

        [TestMethod]
        public void KnownGoodSubwayThiefAndCultistUseTheSharedFactoryWithCaptureExactBytes()
        {
            Identity thief = SimpleChar(ThiefSourceIdentity);
            Identity cultist = SimpleChar(CultistSourceIdentity);
            Identity localPlayer = SimpleChar(LocalPlayerIdentity);
            CapturedEnemyWeaponDefinition thiefWeapon = ThiefWeaponDefinition();
            CapturedEnemyCombatContract cultistContract =
                CapturedTempleOfThreeWindsCombatCatalog.Cultist(
                    26147,
                    CultistSourceIdentity,
                    20);

            Assert.IsTrue(cultistContract.IsCombatReady);
            Assert.AreEqual(CultistSourceIdentity, cultistContract.EvidenceSourceIdentity);
            Assert.IsNotNull(cultistContract.WeaponDefinition);
            Assert.AreNotEqual(
                thiefWeapon.LowId,
                cultistContract.WeaponDefinition.LowId,
                "Subway and Temple weapon values must remain sourced independently.");
            Assert.AreEqual(-1, thiefWeapon.InitialEnergy);
            Assert.AreEqual(15, cultistContract.WeaponDefinition.InitialEnergy);

            AssertHex(
                "3B1D22680000C74A2573BACB000000000B0000C350795B5DB200153008000F424F0000000001060000276A0000000004000401000000170001DADF000002BD00000001000002BE0001DADF000002BF0001DADF0000019C000000010000001AFFFFFFFF00000126000000EB000000D2000000EB00000000",
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    thief,
                    0x00153008,
                    Weapon(unchecked((int)0x2573BACB)),
                    thiefWeapon));
            AssertHex(
                "3B1D22680000C74A257EF84A000000000B0000C3507984B379000E5010000F424F0000000001060000276A000000000000040300000017000232E7000002BD00000018000002BE000232E7000002BF000232E80000019C000000010000001A0000000F00000126000000EB000000D2000000EB00000000",
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    cultist,
                    938000,
                    Weapon(unchecked((int)0x257EF84A)),
                    cultistContract.WeaponDefinition));
            AssertHex(
                "3B1D22680000C74A257EF84A000000000B0000C3507984B379000E5010000F424F0000000001060000276A000000000000040300000017000232E7000002BD00000018000002BE000232E7000002BF000232E80000019C000000010000001A0000000D00000126000000EB000000D2000000EB00000000",
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    cultist,
                    938000,
                    Weapon(unchecked((int)0x257EF84A)),
                    cultistContract.WeaponDefinition,
                    13));

            CapturedEnemyCombatContract thiefStart = ThiefAttackStartContract();
            Assert.IsTrue(thiefStart.IsCombatReady);
            Assert.AreEqual(ThiefSourceIdentity, thiefStart.EvidenceSourceIdentity);
            MessageBody[] thiefAttack =
            {
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(thief, thiefStart),
                CapturedEnemyCombatPacketFactory.CreateAttack(thief, localPlayer, thiefStart),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    thief,
                    localPlayer,
                    9,
                    -1,
                    6,
                    0,
                    3,
                    0,
                    0)
            };
            AssertCapturedOrder(thiefAttack);
            AssertHex(
                "1D3C0F1C0000C350795B5DB200000003F10000002000000020000000200000002000000000",
                thiefAttack[0]);
            AssertHex(
                "284940700000C350795B5DB2000000C3507944C06500",
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    thief,
                    SimpleChar(unchecked((int)0x7944C065)),
                    thiefStart));
            AssertHex(
                "46002F160000C350795B5DB20000000009FFFFFFFF000000060000C3507944C065000000000000000300000000",
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    thief,
                    SimpleChar(unchecked((int)0x7944C065)),
                    9,
                    -1,
                    6,
                    0,
                    3,
                    0,
                    0));

            MessageBody[] cultistAttack =
            {
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(cultist, cultistContract),
                CapturedEnemyCombatPacketFactory.CreateAttack(cultist, localPlayer, cultistContract),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    cultist,
                    localPlayer,
                    15,
                    14,
                    cultistContract.AttackInfoWeaponSlot,
                    cultistContract.AttackInfoUnknown,
                    cultistContract.AttackInfoHitType,
                    cultistContract.AttackInfoWeaponInstance,
                    cultistContract.AttackInfoN3Unknown)
            };
            AssertCapturedOrder(cultistAttack);
            AssertHex(
                "1D3C0F1C0000C3507984B37900000003F10000013100000131000001310000000C00000000",
                cultistAttack[0]);
            AssertHex(
                "284940700000C3507984B379000000C35070CBBEF300",
                cultistAttack[1]);
            AssertHex(
                "46002F160000C3507984B379000000000F0000000E000000060000C35070CBBEF3000000000000000300000000",
                cultistAttack[2]);
            AssertHex(
                "46002F160000C3507984B37900000000120000000D000000060000C35070CBBEF3000000000000000300000000",
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    cultist,
                    localPlayer,
                    18,
                    13,
                    cultistContract.AttackInfoWeaponSlot,
                    cultistContract.AttackInfoUnknown,
                    cultistContract.AttackInfoHitType,
                    cultistContract.AttackInfoWeaponInstance,
                    cultistContract.AttackInfoN3Unknown));
        }

        [TestMethod]
        public void CultistResolutionRejectsMissingNearestAndCrossEnemyEvidence()
        {
            Assert.IsFalse(CapturedTempleOfThreeWindsCombatCatalog.Cultist(26147, 20).IsCombatReady);
            Assert.IsFalse(
                CapturedTempleOfThreeWindsCombatCatalog.Cultist(
                    26147,
                    CultistSourceIdentity,
                    21).IsCombatReady);
            Assert.IsFalse(
                CapturedTempleOfThreeWindsCombatCatalog.Cultist(
                    26149,
                    CultistSourceIdentity,
                    20).IsCombatReady);
            Assert.IsFalse(
                CapturedTempleOfThreeWindsCombatCatalog.Cultist(
                    26147,
                    unchecked((int)0x7984B37A),
                    20).IsCombatReady);
            Assert.IsFalse(CapturedTempleOfThreeWindsCombatCatalog.EternalSentinel(18).IsCombatReady);
            Assert.IsFalse(
                CapturedTempleOfThreeWindsCombatCatalog.EternalSentinel(
                    unchecked((int)0x7983FB93),
                    18).IsCombatReady);
            Assert.IsFalse(
                CapturedTempleOfThreeWindsCombatCatalog.Cultist(
                        26147,
                        CultistSourceIdentity,
                        20)
                    .WithCapturedWeapon(ThiefWeaponDefinition())
                    .IsCombatReady,
                "A WIFU from another actor must not certify the Cultist packet fields.");
        }

        [TestMethod]
        public void CapturedNumericHitWireValueFourIsPreservedWithoutEnumNormalization()
        {
            AttackInfoMessage critical = CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                SimpleChar(unchecked((int)0x7983FB93)),
                SimpleChar(LocalPlayerIdentity),
                58,
                18,
                6,
                0,
                4,
                0,
                0);

            Assert.AreEqual(4, critical.Unknown5);
            AssertHex(
                "46002F160000C3507983FB93000000003A00000012000000060000C35070CBBEF3000000000000000400000000",
                critical);
            Assert.IsFalse(
                CapturedTempleOfThreeWindsCombatCatalog.Cultist(
                    26149,
                    unchecked((int)0x7983FB93),
                    29).IsCombatReady,
                "A critical-only observation must not be normalized into an ordinary-hit contract.");
        }

        [TestMethod]
        public void TempleOrdinaryCoverageHasFourteenExactContractsAndQuarantinesTheRest()
        {
            var provider = new CapturedTempleOfThreeWindsContentProvider();
            Dictionary<string, OrdinaryEnemyProfile> profiles = provider.GetProfiles()
                .ToDictionary(value => value.ProfileKey, StringComparer.Ordinal);
            OrdinaryEnemySpawnDefinition[] spawns = provider.GetSpawns();
            var ready = new List<OrdinaryEnemySpawnDefinition>();
            var quarantined = new List<OrdinaryEnemySpawnDefinition>();

            foreach (OrdinaryEnemySpawnDefinition spawn in spawns)
            {
                CapturedEnemyCombatContract contract = profiles[spawn.ProfileKey].Combat.ResolveContract(
                    spawn.SourceIdentity,
                    spawn.Level);
                if (contract.IsCombatReady)
                {
                    Assert.AreEqual(spawn.SourceIdentity, contract.EvidenceSourceIdentity);
                    Assert.IsNotNull(contract.WeaponDefinition);
                    Assert.AreEqual(
                        spawn.SourceIdentity,
                        contract.WeaponDefinition.EvidenceSourceIdentity);
                    Assert.AreEqual(
                        CapturedTempleOfThreeWindsCombatCatalog.CultistFirstSuccessfulHitDelaySeconds,
                        contract.FirstHitDelaySeconds,
                        0.00000001);
                    Assert.IsFalse(contract.UsesEquippedWeaponDamage);
                    Assert.AreEqual(15, contract.MinDamage);
                    Assert.AreEqual(32, contract.MaxDamage);
                    Assert.AreEqual(0, contract.CapturedDamageBonus);
                    Assert.AreEqual(
                        CapturedTempleOfThreeWindsCombatCatalog.CultistRechargeSeconds,
                        contract.RechargeSeconds,
                        0.00000001);
                    ready.Add(spawn);
                }
                else
                {
                    Assert.IsTrue(contract.IsQuarantined);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(contract.QuarantineReason));
                    quarantined.Add(spawn);
                }
            }

            Assert.AreEqual(153, spawns.Length);
            Assert.AreEqual(14, ready.Count);
            Assert.AreEqual(139, quarantined.Count);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    unchecked((int)0x79834EC1), unchecked((int)0x79834EC3),
                    unchecked((int)0x79834ECC), unchecked((int)0x79834ECD),
                    unchecked((int)0x79834ECF), unchecked((int)0x7983FB96),
                    unchecked((int)0x7983FB98), unchecked((int)0x7983FB9B),
                    unchecked((int)0x7983FBDF), unchecked((int)0x7983FC37),
                    unchecked((int)0x7984B374), unchecked((int)0x7984B375),
                    unchecked((int)0x7984B379), unchecked((int)0x7984B37C)
                },
                ready.Select(value => value.SourceIdentity).ToArray());
        }

        [TestMethod]
        public void ActiveDungeonProfileAuditAllowsOnlySourceOwnedCompleteContracts()
        {
            var temple = new CapturedTempleOfThreeWindsContentProvider();
            var catalog = new OrdinaryEnemyCatalog(
                new CapturedSubwayContentProvider(),
                new CapturedSubwayOrdinaryContentProvider(),
                temple);
            Dictionary<string, OrdinaryEnemyProfile> profiles = catalog.GetProfiles()
                .ToDictionary(value => value.ProfileKey, StringComparer.Ordinal);
            OrdinaryEnemySpawnDefinition[] subwaySpawns = catalog.GetSpawns()
                .Where(value => value.PlayfieldInstance == 127)
                .ToArray();
            OrdinaryEnemySpawnDefinition[] templeSpawns = catalog.GetSpawns()
                .Where(value => value.PlayfieldInstance == 1931)
                .ToArray();

            CapturedEnemyCombatContract[] subwayContracts = subwaySpawns.Select(
                spawn => profiles[spawn.ProfileKey].Combat.ResolveContract(
                    spawn.SourceIdentity,
                    spawn.Level)).ToArray();
            CapturedEnemyCombatContract[] templeContracts = templeSpawns.Select(
                spawn => profiles[spawn.ProfileKey].Combat.ResolveContract(
                    spawn.SourceIdentity,
                    spawn.Level)).ToArray();

            Assert.AreEqual(322, subwaySpawns.Length);
            Assert.AreEqual(1, subwayContracts.Count(value => value.IsCombatReady));
            Assert.AreEqual(321, subwayContracts.Count(value => value.IsQuarantined));
            Assert.IsTrue(
                subwaySpawns.Zip(
                        subwayContracts,
                        (spawn, contract) => new { Spawn = spawn, Contract = contract })
                    .Where(value => value.Contract.IsCombatReady)
                    .All(value => value.Spawn.SourceIdentity == 0x7953AEA5));
            Assert.AreEqual(153, templeSpawns.Length);
            Assert.AreEqual(14, templeContracts.Count(value => value.IsCombatReady));
            Assert.AreEqual(139, templeContracts.Count(value => value.IsQuarantined));

            CapturedEnemyCombatContract[] sourceUnboundSubwayEncounters =
            {
                CapturedSubwayCombatCatalog.For("Eumenides", 203726, 20),
                CapturedSubwayCombatCatalog.For("Vergil Aeneid", 203748, 23),
                CapturedSubwayCombatCatalog.For("Abmouth Supremus", 155962, 30),
                CapturedSubwayCombatCatalog.For("Infector", 31909, 20)
            };
            Assert.IsTrue(sourceUnboundSubwayEncounters.All(value => value.IsQuarantined));

            CapturedEnemyCombatContract[] sourceUnboundTempleEncounters =
            {
                CapturedTempleOfThreeWindsCombatCatalog.DefenderOfTheThree(),
                CapturedTempleOfThreeWindsCombatCatalog.WindcallerYatila(),
                CapturedTempleOfThreeWindsCombatCatalog.ReverendGulard(),
                CapturedTempleOfThreeWindsCombatCatalog.ReAnimator(),
                CapturedTempleOfThreeWindsCombatCatalog.AcolyteBetany(),
                CapturedTempleOfThreeWindsCombatCatalog.TheCurator(),
                CapturedTempleOfThreeWindsCombatCatalog.NematetTheCustodianOfTime(),
                CapturedTempleOfThreeWindsCombatCatalog.GuardianOfTomorrow(),
                CapturedTempleOfThreeWindsCombatCatalog.GartuaTheDoorkeeper(),
                CapturedTempleOfThreeWindsCombatCatalog.ReanimatedCorpse()
            };
            Assert.IsTrue(sourceUnboundTempleEncounters.All(value => value.IsQuarantined));
        }

        [TestMethod]
        public void SharedRuntimeOwnsCapturedPacketSemanticsAndSyntheticHitChatIsAbsent()
        {
            string repositoryRoot = FindRepositoryRoot();
            string coreDirectory = Path.Combine(
                repositoryRoot,
                "AORebirth",
                "Server",
                "ZoneEngine",
                "Core");
            string coordinator = File.ReadAllText(
                Path.Combine(
                    coreDirectory,
                    "Playfields",
                    "NpcCombatTickCoordinator.cs"));
            string visibility = File.ReadAllText(
                Path.Combine(
                    coreDirectory,
                    "Packets",
                    "WeaponItemFullUpdate.cs"));
            string functionHit = File.ReadAllText(
                Path.Combine(
                    coreDirectory,
                    "Functions",
                    "GameFunctions",
                    "hit.cs"));
            string templeCatalog = File.ReadAllText(
                Path.Combine(
                    coreDirectory,
                    "Playfields",
                    "CapturedTempleOfThreeWindsCombatCatalog.cs"));
            string marcus = File.ReadAllText(
                Path.Combine(
                    coreDirectory,
                    "Playfields",
                    "MarcusPadAmbientCombat.cs"));
            string contractRuntime = File.ReadAllText(
                Path.Combine(
                    coreDirectory,
                    "Playfields",
                    "CapturedEnemyCombatContract.cs"));
            string npcRuntime = File.ReadAllText(
                Path.Combine(
                    coreDirectory,
                    "Playfields",
                    "NPCRuntimeService.cs"));
            string[] otherImplementedHostileEntryPoints =
            {
                Path.Combine(coreDirectory, "Missions", "MissionInstanceMobCombat.cs"),
                Path.Combine(coreDirectory, "Playfields", "AlexAreaMobRuntime.cs"),
                Path.Combine(coreDirectory, "Playfields", "AreteFinishCaptureMobRuntime.cs"),
                Path.Combine(coreDirectory, "Playfields", "CapturedAreteRobotSpawnOrchestrator.cs"),
                Path.Combine(coreDirectory, "Playfields", "CapturedSubwayVendorRuntimeService.cs"),
                Path.Combine(coreDirectory, "Playfields", "JunkyardCleaningRobotRuntime.cs"),
                Path.Combine(coreDirectory, "Playfields", "NascenceCoreHecklerSpawnOrchestrator.cs"),
                Path.Combine(coreDirectory, "Playfields", "NascenceLifeSpawn.cs"),
                Path.Combine(coreDirectory, "Playfields", "LoreleiOasisMobRuntime.cs"),
                Path.Combine(coreDirectory, "Playfields", "MarcusPadAmbientCombat.cs"),
                Path.Combine(coreDirectory, "Playfields", "RomeBlueCitySpawn.cs"),
                Path.Combine(coreDirectory, "Playfields", "ThrakOmniGardenSpawn.cs"),
                Path.Combine(coreDirectory, "Thrak", "Quests", "ThrakGardenKeySilvertailTransform.cs")
            };
            string[] combatSources = Directory.GetFiles(
                coreDirectory,
                "*.cs",
                SearchOption.AllDirectories);
            string[] sourceOwnedWeaponCallers = combatSources.Where(
                path => File.ReadAllText(path).Contains(".WithCapturedWeapon(")).ToArray();

            Assert.IsTrue(coordinator.Contains("CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon("));
            Assert.IsTrue(coordinator.Contains("CapturedEnemyCombatPacketFactory.CreateAttack("));
            Assert.IsTrue(coordinator.Contains("CapturedEnemyCombatPacketFactory.CreateAttackInfo("));
            Assert.IsTrue(coordinator.Contains("CreateCapturedCleaningRobotSpecialAttacks(),"));
            Assert.IsTrue(visibility.Contains("CapturedEnemyCombatPacketFactory.CreateWeaponDefinition("));
            Assert.IsFalse(coordinator.Contains("SendIncomingHitChatIfPlayer"));
            Assert.IsFalse(coordinator.Contains("hit you for"));
            Assert.IsFalse(templeCatalog.Contains("new AttackInfoMessage"));
            Assert.IsFalse(templeCatalog.Contains("new AttackMessage"));
            Assert.IsFalse(templeCatalog.Contains("new SpecialAttackWeaponMessage"));
            Assert.IsTrue(marcus.Contains("CapturedEnemyCombatContract.Unresolved("));
            Assert.IsFalse(marcus.Contains("new AttackInfoMessage"));
            Assert.IsFalse(marcus.Contains("new AttackMessage"));
            Assert.IsTrue(contractRuntime.Contains("case CapturedEnemyAttackModel.FixedAttackInfo:"));
            Assert.IsTrue(contractRuntime.Contains("controller.AiProfile = NpcAiProfile.Passive;"));
            Assert.IsTrue(contractRuntime.Contains("CapturedEnemyCombatRuntimeRegistry.Register(character.Identity.Instance, contract);"));
            Assert.IsTrue(contractRuntime.Contains("TryGetCapturedWeaponEnergy"));
            Assert.IsTrue(contractRuntime.Contains("HasCapturedRequiredPacketFields"));
            Assert.IsTrue(coordinator.Contains("&& !registeredCapturedContract.IsCombatReady"));
            Assert.IsTrue(coordinator.Contains("required captured weapon is missing from the live inventory"));
            Assert.IsTrue(coordinator.Contains("if (movementAttackSource == null)"));
            Assert.IsTrue(coordinator.Contains("if (attackSource == null)"));
            Assert.IsTrue(coordinator.Contains("!capturedContract.MatchesCapturedWeapon(weapon)"));
            Assert.IsTrue(coordinator.Contains("captured weapon Energy is exhausted or unavailable"));
            Assert.IsTrue(coordinator.Contains("ValidateRequiredCapturedWeapon("));
            Assert.IsTrue(visibility.Contains("TryGetCapturedWeaponEnergy("));
            Assert.IsTrue(visibility.Contains("CapturedEnemyCombatRuntimeRegistry.QuarantineRuntime("));
            Assert.IsTrue(visibility.Contains("CapturedEnemyCombatRuntime.TryValidateLiveCapturedWeapon("));
            Assert.IsTrue(contractRuntime.Contains("internal bool MatchesCapturedWeapon(IItem item)"));
            Assert.IsTrue(contractRuntime.Contains("TryGetCapturedWeaponItem"));
            Assert.IsTrue(contractRuntime.Contains("ReferenceEquals(item, registeredItem)"));
            Assert.IsTrue(contractRuntime.Contains("currentEnergy != -1 && currentEnergy <= 0"));
            Assert.IsTrue(contractRuntime.Contains("captured weapon Energy is exhausted"));
            Assert.IsTrue(functionHit.Contains("CapturedEnemyCombatFunctionHitQuarantined"));
            Assert.IsTrue(npcRuntime.Contains("Captured enemy taunt refused"));
            Assert.IsTrue(npcRuntime.Contains("!NpcAiProfiles.CanRetaliate(npcController.AiProfile)"));
            Assert.IsTrue(otherImplementedHostileEntryPoints.All(
                path => File.ReadAllText(path).Contains("CapturedEnemyCombatRuntime.Prepare")));
            Assert.AreEqual(2, sourceOwnedWeaponCallers.Length);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "CapturedEnemyCombatContract.cs",
                    "CapturedTempleOfThreeWindsCombatCatalog.cs"
                },
                sourceOwnedWeaponCallers.Select(Path.GetFileName).ToArray());
            Assert.IsFalse(combatSources.Any(
                path => File.ReadAllText(path).Contains("WithEvidenceSource(")));
        }

        private static CapturedEnemyCombatContract ThiefAttackStartContract()
        {
            return CapturedSubwayCombatCatalog.For("Thief", 26092, 5);
        }

        private static CapturedEnemyWeaponDefinition ThiefWeaponDefinition()
        {
            return new CapturedEnemyWeaponDefinition(
                "20260711-170337 raw 155/156,301/302,480/564/654",
                ThiefSourceIdentity,
                0,
                11,
                6,
                1000015,
                0,
                262,
                new[]
                {
                    WeaponStat(CharacterStat.Flags, 67109889),
                    WeaponStat(CharacterStat.StaticInstance, 121567),
                    WeaponStat(CharacterStat.ACGItemLevel, 1),
                    WeaponStat(CharacterStat.ACGItemTemplateID, 121567),
                    WeaponStat(CharacterStat.ACGItemTemplateID2, 121567),
                    WeaponStat(CharacterStat.MultipleCount, 1),
                    WeaponStat(CharacterStat.Energy, -1),
                    WeaponStat(CharacterStat.AttackDelay, 235),
                    WeaponStat(CharacterStat.RechargeDelay, 235)
                },
                0);
        }

        private static CapturedEnemyWeaponStatDefinition WeaponStat(CharacterStat stat, int value)
        {
            return new CapturedEnemyWeaponStatDefinition(stat, unchecked((uint)value));
        }

        private static Identity SimpleChar(int instance)
        {
            return new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
        }

        private static Identity Weapon(int instance)
        {
            return new Identity { Type = (IdentityType)0xC74A, Instance = instance };
        }

        private static void AssertCapturedOrder(MessageBody[] messages)
        {
            Assert.AreEqual(3, messages.Length);
            Assert.IsInstanceOfType(messages[0], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(messages[1], typeof(AttackMessage));
            Assert.IsInstanceOfType(messages[2], typeof(AttackInfoMessage));
        }

        private static void AssertHex(string expected, MessageBody message)
        {
            Assert.AreEqual(expected, BitConverter.ToString(Serialize(message)).Replace("-", string.Empty));
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

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("AORebirth repository root was not found.");
        }
    }
}
