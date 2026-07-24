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
        public void CapturedDamageObservationsAdvanceIndependentlyPerActorAndAttackStream()
        {
            var cursor = new CapturedIntObservationCursor();
            int[] leftStream = { 11, 12 };
            int[] rightStream = { 21, 22, 23 };

            Assert.AreEqual(11, cursor.Select(100, leftStream));
            Assert.AreEqual(21, cursor.Select(100, rightStream));
            Assert.AreEqual(12, cursor.Select(100, leftStream));
            Assert.AreEqual(22, cursor.Select(100, rightStream));
            Assert.AreEqual(11, cursor.Select(200, leftStream));

            cursor.Clear(100);
            Assert.AreEqual(11, cursor.Select(100, leftStream));
            Assert.AreEqual(21, cursor.Select(100, rightStream));
        }

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
            AssertHex(
                "3B1D22680000C74A257EF84A000000000B0000C3507984B379000E5010000F424F0000000001060000276A000000000000040300000017000232E7000002BD00000018000002BE000232E7000002BF000232E80000019C000000070000001A0000000D00000126000000EB000000D2000000EB00000000",
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    cultist,
                    938000,
                    Weapon(unchecked((int)0x257EF84A)),
                    cultistContract.WeaponDefinition,
                    13,
                    7));

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
        public void AzturRoomBossesUseTheSharedFactoryWithCaptureExactBytes()
        {
            Identity localPlayer = SimpleChar(LocalPlayerIdentity);
            CapturedEnemyCombatContract uklesh =
                CapturedTempleOfThreeWindsCombatCatalog.UkleshTheFrozen();
            CapturedEnemyCombatContract khalum;
            CapturedEnemyCombatContract aztur;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    1931,
                    "Khalum",
                    95352,
                    73,
                    unchecked((int)0x7988C14Du),
                    CapturedTempleOfThreeWindsCombatCatalog.Khalum(),
                    out khalum,
                    out failure),
                failure);
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    1931,
                    "Aztur the Immortal",
                    159966,
                    74,
                    unchecked((int)0x7988C153u),
                    CapturedTempleOfThreeWindsCombatCatalog.AzturTheImmortal(),
                    out aztur,
                    out failure),
                failure);

            Identity ukleshIdentity = SimpleChar(unchecked((int)0x7987F730u));
            MessageBody[] ukleshAttack =
            {
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    ukleshIdentity,
                    uklesh,
                    0),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    ukleshIdentity,
                    localPlayer,
                    uklesh),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    ukleshIdentity,
                    localPlayer,
                    127,
                    -1,
                    0,
                    0,
                    3,
                    1280662101,
                    0)
            };
            AssertCapturedOrder(ukleshAttack);
            AssertHex(
                "1D3C0F1C0000C3507987F7300000000BD300032E2600032E27504B4457504B4457000320D4000320D54C555A554C555A550000022700000227000002270000002A00000000",
                ukleshAttack[0]);
            AssertHex(
                "284940700000C3507987F730000000C35070CBBEF300",
                ukleshAttack[1]);
            AssertHex(
                "46002F160000C3507987F730000000007FFFFFFFFF000000000000C35070CBBEF300000000000000034C555A55",
                ukleshAttack[2]);

            Identity khalumIdentity = SimpleChar(unchecked((int)0x7988C14Du));
            MessageBody[] khalumAttack =
            {
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    khalumIdentity,
                    khalum,
                    0),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    khalumIdentity,
                    localPlayer,
                    khalum),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    khalumIdentity,
                    localPlayer,
                    58,
                    -1,
                    1,
                    0,
                    3,
                    1297107795,
                    0)
            };
            AssertCapturedOrder(khalumAttack);
            AssertHex(
                "1D3C0F1C0000C3507988C14D0000000BD300032DAA00032DAB4D504B534D504B5300032DAE00032DA85346544E5346544E0000022700000227000002270000002A00000000",
                khalumAttack[0]);
            AssertHex(
                "284940700000C3507988C14D000000C35070CBBEF300",
                khalumAttack[1]);
            AssertHex(
                "46002F160000C3507988C14D000000003AFFFFFFFF000000010000C35070CBBEF300000000000000034D504B53",
                khalumAttack[2]);

            Identity azturIdentity = SimpleChar(unchecked((int)0x7988C153u));
            MessageBody[] azturAttack =
            {
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    azturIdentity,
                    aztur,
                    0),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    azturIdentity,
                    localPlayer,
                    aztur),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    azturIdentity,
                    localPlayer,
                    359,
                    -1,
                    3,
                    0,
                    3,
                    1179993922,
                    0)
            };
            AssertCapturedOrder(azturAttack);
            AssertHex(
                "1D3C0F1C0000C3507988C1530000000FC4000329DF000329E04655474246554742000329DC000329DD5948555559485555000329D9000329DA4B4842434B4842430000034800000348000003480000034800000000",
                azturAttack[0]);
            AssertHex(
                "284940700000C3507988C153000000C35070CBBEF300",
                azturAttack[1]);
            AssertHex(
                "46002F160000C3507988C1530000000167FFFFFFFF000000030000C35070CBBEF3000000000000000346554742",
                azturAttack[2]);
        }

        [TestMethod]
        public void WorkmanStrikerStableProfileUsesTheCapturedSharedPacketSequence()
        {
            const string profileId = "0ab4af8e83e1830c-4fb632d821975655";
            const int runtimeSourceIdentity = unchecked((int)0x79545219);
            CapturedEnemyCombatProfileDefinition profile =
                CapturedEnemyCombatProfileCatalog.GetProfilesForTests().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Workman Striker",
                    203854,
                    16,
                    runtimeSourceIdentity,
                    CapturedEnemyCombatContract.EquippedWeapon(
                        "active subway Workman Striker source 0x79545219",
                        122905,
                        122906,
                        19,
                        6),
                    out resolved,
                    out failure),
                failure);

            CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets.Single(
                value => value.OwnerIdentity == resolved.EvidenceSourceIdentity
                         && value.Energy == resolved.WeaponDefinition.InitialEnergy
                         && value.MultipleCount
                            == resolved.WeaponDefinition.SignedStatValue(
                                CharacterStat.MultipleCount));
            CapturedEnemySpecialAttackWeaponPacketFixture saw =
                fixture.SpecialAttackWeaponPackets.Single(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.Unknown5 == resolved.SpecialAttackWeaponUnknown5);
            CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.Single(
                value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
            CapturedEnemyAttackInfoPacketFixture attackInfo =
                fixture.AttackInfoPackets.First(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                             && value.DamageTypeWire == resolved.AttackInfoUnknown
                             && value.HitTypeWire == resolved.AttackInfoHitType
                             && value.WeaponInstance == resolved.AttackInfoWeaponInstance
                             && value.N3Unknown == resolved.AttackInfoN3Unknown
                             && resolved.CapturedDamageObservations.Contains(value.Amount));

            MessageBody[] capturedSequence =
            {
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                    weapon.PlayfieldId,
                    IdentityOf(weapon.WeaponIdentityType, weapon.WeaponIdentityInstance),
                    resolved.WeaponDefinition,
                    weapon.Energy,
                    weapon.MultipleCount),
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    IdentityOf(saw.SourceType, saw.SourceIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    IdentityOf(attack.SourceType, attack.SourceIdentity),
                    IdentityOf(attack.TargetType, attack.TargetIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                    IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                    attackInfo.Amount,
                    attackInfo.Ammo,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown)
            };

            Assert.AreEqual(4, capturedSequence.Length);
            Assert.IsInstanceOfType(capturedSequence[0], typeof(WeaponItemFullUpdateMessage));
            Assert.IsInstanceOfType(capturedSequence[1], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(capturedSequence[2], typeof(AttackMessage));
            Assert.IsInstanceOfType(capturedSequence[3], typeof(AttackInfoMessage));
            AssertHex(weapon.BodyHex, capturedSequence[0]);
            AssertHex(saw.BodyHex, capturedSequence[1]);
            AssertHex(attack.BodyHex, capturedSequence[2]);
            AssertHex(attackInfo.BodyHex, capturedSequence[3]);
        }

        [TestMethod]
        public void WorkmanStrikerProductionQlChangesOnlyTheCapturedWifuQlField()
        {
            const string profileId = "5db002948ad46e4a-0278a5de1cc46a00";
            const int runtimeSourceIdentity = unchecked((int)0x79545000);
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Workman Striker",
                    203854,
                    14,
                    runtimeSourceIdentity,
                    CapturedEnemyCombatContract.EquippedWeapon(
                        "active Workman Striker QL11 atomic generation",
                        122905,
                        122906,
                        11,
                        6)
                        .WithProductionWeaponQuality(),
                    out resolved,
                    out failure),
                failure);

            CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets[0];
            CapturedEnemySpecialAttackWeaponPacketFixture saw =
                fixture.SpecialAttackWeaponPackets[0];
            CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets[0];
            CapturedEnemyAttackInfoPacketFixture attackInfo =
                fixture.AttackInfoPackets[0];
            MessageBody[] sequence =
            {
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                    weapon.PlayfieldId,
                    IdentityOf(weapon.WeaponIdentityType, weapon.WeaponIdentityInstance),
                    resolved.WeaponDefinition,
                    weapon.Energy,
                    weapon.MultipleCount),
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    IdentityOf(saw.SourceType, saw.SourceIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    IdentityOf(attack.SourceType, attack.SourceIdentity),
                    IdentityOf(attack.TargetType, attack.TargetIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                    IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                    attackInfo.Amount,
                    attackInfo.Ammo,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown)
            };

            Assert.AreEqual(4, sequence.Length);
            AssertHex(
                "3B1D22680000C74A2571391A000000000B0000C3507953AA1600122002000F424F0000000001060000276A0000000000000403000000170001E019000002BD0000000B000002BE0001E019000002BF0001E01A0000019C000000010000001AFFFFFFFF00000126000000EB000000D2000000EB00000000",
                sequence[0]);
            AssertHex(saw.BodyHex, sequence[1]);
            AssertHex(attack.BodyHex, sequence[2]);
            AssertHex(attackInfo.BodyHex, sequence[3]);
        }

        [TestMethod]
        public void AlreadyAuthorizedShadowUsesItsExactCapturedPacketSequenceWithoutARangeField()
        {
            const string profileId = "469eedefbd2e7efe-83d6c6ca8cd6c3d2";
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Shadow",
                    30464,
                    14,
                    0,
                    CapturedEnemyCombatContract.Unresolved(
                        "already-authorized shared packet-path test",
                        true),
                    out resolved,
                    out failure),
                failure);
            Assert.IsTrue(resolved.IsCombatReady);
            Assert.IsFalse(resolved.CapturedAttackRange.HasValue);
            Assert.IsFalse(resolved.CapturedUsesEquippedWeapon);

            CapturedEnemySpecialAttackWeaponPacketFixture saw =
                fixture.SpecialAttackWeaponPackets.First(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.Unknown5 == resolved.SpecialAttackWeaponUnknown5);
            CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.First(
                value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
            CapturedEnemyAttackInfoPacketFixture attackInfo =
                fixture.AttackInfoPackets.First(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                             && value.DamageTypeWire == resolved.AttackInfoUnknown
                             && value.HitTypeWire == resolved.AttackInfoHitType
                             && value.WeaponInstance == resolved.AttackInfoWeaponInstance
                             && value.N3Unknown == resolved.AttackInfoN3Unknown
                             && resolved.CapturedDamageObservations.Contains(value.Amount));

            MessageBody[] capturedSequence =
            {
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    IdentityOf(saw.SourceType, saw.SourceIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    IdentityOf(attack.SourceType, attack.SourceIdentity),
                    IdentityOf(attack.TargetType, attack.TargetIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                    IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                    attackInfo.Amount,
                    attackInfo.Ammo,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown)
            };

            AssertCapturedOrder(capturedSequence);
            AssertHex(saw.BodyHex, capturedSequence[0]);
            AssertHex(attack.BodyHex, capturedSequence[1]);
            AssertHex(attackInfo.BodyHex, capturedSequence[2]);
        }

        [TestMethod]
        public void DiscardedPetSawStateTransitionsReplayInCapturedOrderPerActor()
        {
            const string profileId = "95d366ebb4f855e2-9bcb7a58208cf1e0";
            int[] expectedStates = { 0, 0, 49, 49, 0, 46, 46, 46, 46, 40 };
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Discarded Pet",
                    17720,
                    10,
                    0,
                    CapturedEnemyCombatContract.Unresolved(
                        "captured mutable SAW replay test",
                        true),
                    out resolved,
                    out failure),
                failure);
            CollectionAssert.AreEqual(
                expectedStates,
                resolved.CapturedSpecialAttackWeaponUnknown5Observations);
            CollectionAssert.AreEqual(
                expectedStates,
                fixture.SpecialAttackWeaponPackets.Select(value => value.Unknown5).ToArray());

            var cursor = new CapturedIntObservationCursor();
            for (int index = 0; index < fixture.SpecialAttackWeaponPackets.Length; index++)
            {
                CapturedEnemySpecialAttackWeaponPacketFixture packet =
                    fixture.SpecialAttackWeaponPackets[index];
                int selected = cursor.Select(
                    5001,
                    resolved.CapturedSpecialAttackWeaponUnknown5Observations);
                Assert.AreEqual(expectedStates[index], selected);
                AssertHex(
                    packet.BodyHex,
                    CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                        IdentityOf(packet.SourceType, packet.SourceIdentity),
                        resolved,
                        selected));
            }

            Assert.AreEqual(
                expectedStates[0],
                cursor.Select(5001, resolved.CapturedSpecialAttackWeaponUnknown5Observations));
            Assert.AreEqual(
                expectedStates[0],
                cursor.Select(5002, resolved.CapturedSpecialAttackWeaponUnknown5Observations));
        }

        [TestMethod]
        public void ReanimatedCorpseAnchorProfilesUseTheCapturedSharedPacketSequence()
        {
            var expectedProfiles = new[]
            {
                new
                {
                    SourceIdentity =
                        CapturedTempleOfThreeWindsCombatCatalog
                            .ReanimatedFirstAnchorCaptureSourceIdentity,
                    ProfileId = "74af62ea08cc19d6-7757e8ce980f0cf3"
                },
                new
                {
                    SourceIdentity =
                        CapturedTempleOfThreeWindsCombatCatalog
                            .ReanimatedSecondAnchorCaptureSourceIdentity,
                    ProfileId = "74af62ea08cc19d6-2c2762baa2d8ec8d"
                }
            };

            foreach (var expected in expectedProfiles)
            {
                CapturedEnemyCombatPacketFixture fixture =
                    CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                        value => value.ProfileId == expected.ProfileId);
                CapturedEnemyCombatContract resolved;
                string failure;
                Assert.IsTrue(
                    CapturedEnemyCombatProfileCatalog.TryResolve(
                        1931,
                        "Reanimated Corpse",
                        41690,
                        18,
                        expected.SourceIdentity,
                        CapturedTempleOfThreeWindsCombatCatalog.ReanimatedCorpse(
                            expected.SourceIdentity),
                        out resolved,
                        out failure),
                    failure);

                CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets.Single(
                    value => value.OwnerIdentity == resolved.EvidenceSourceIdentity
                             && value.Energy == resolved.WeaponDefinition.InitialEnergy
                             && value.MultipleCount
                                == resolved.WeaponDefinition.SignedStatValue(
                                    CharacterStat.MultipleCount));
                CapturedEnemySpecialAttackWeaponPacketFixture saw =
                    fixture.SpecialAttackWeaponPackets.Single(
                        value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                                 && value.Unknown5 == resolved.SpecialAttackWeaponUnknown5);
                CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.Single(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
                CapturedEnemyAttackInfoPacketFixture attackInfo =
                    fixture.AttackInfoPackets.First(
                        value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                                 && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                                 && value.DamageTypeWire == resolved.AttackInfoUnknown
                                 && value.HitTypeWire == resolved.AttackInfoHitType
                                 && value.WeaponInstance == resolved.AttackInfoWeaponInstance
                                 && value.N3Unknown == resolved.AttackInfoN3Unknown
                                 && resolved.CapturedDamageObservations.Contains(value.Amount));

                MessageBody[] capturedSequence =
                {
                    CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                        IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                        weapon.PlayfieldId,
                        IdentityOf(weapon.WeaponIdentityType, weapon.WeaponIdentityInstance),
                        resolved.WeaponDefinition,
                        weapon.Energy,
                        weapon.MultipleCount),
                    CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                        IdentityOf(saw.SourceType, saw.SourceIdentity),
                        resolved),
                    CapturedEnemyCombatPacketFactory.CreateAttack(
                        IdentityOf(attack.SourceType, attack.SourceIdentity),
                        IdentityOf(attack.TargetType, attack.TargetIdentity),
                        resolved),
                    CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                        IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                        IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                        attackInfo.Amount,
                        attackInfo.Ammo,
                        resolved.AttackInfoWeaponSlot,
                        resolved.AttackInfoUnknown,
                        resolved.AttackInfoHitType,
                        resolved.AttackInfoWeaponInstance,
                        resolved.AttackInfoN3Unknown)
                };

                Assert.AreEqual(4, capturedSequence.Length);
                Assert.IsInstanceOfType(capturedSequence[0], typeof(WeaponItemFullUpdateMessage));
                Assert.IsInstanceOfType(capturedSequence[1], typeof(SpecialAttackWeaponMessage));
                Assert.IsInstanceOfType(capturedSequence[2], typeof(AttackMessage));
                Assert.IsInstanceOfType(capturedSequence[3], typeof(AttackInfoMessage));
                AssertHex(weapon.BodyHex, capturedSequence[0]);
                AssertHex(saw.BodyHex, capturedSequence[1]);
                AssertHex(attack.BodyHex, capturedSequence[2]);
                AssertHex(attackInfo.BodyHex, capturedSequence[3]);
            }
        }

        [TestMethod]
        public void EumenidesQ20ProfileUsesTheCapturedSharedPacketSequence()
        {
            const string profileId = "8b40ecdf74edf8a9-f3f54c2f107b40b4";
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Eumenides",
                    203726,
                    20,
                    0,
                    CapturedEnemyCombatContract.EquippedWeapon(
                        "Eumenides QL20 generated-profile selector",
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponLowTemplate,
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponHighTemplate,
                        NpcCombatAttackRules.CapturedSubwayEumenidesWeaponQuality,
                        6,
                        requiresDamageLineOfSight: true),
                    out resolved,
                    out failure),
                failure);

            CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets.Single(
                value => value.OwnerIdentity == resolved.EvidenceSourceIdentity
                         && value.Energy == resolved.WeaponDefinition.InitialEnergy
                         && value.MultipleCount
                            == resolved.WeaponDefinition.SignedStatValue(
                                CharacterStat.MultipleCount));
            CapturedEnemySpecialAttackWeaponPacketFixture saw =
                fixture.SpecialAttackWeaponPackets.Single(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.Unknown5 == resolved.SpecialAttackWeaponUnknown5);
            CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.Single(
                value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
            CapturedEnemyAttackInfoPacketFixture attackInfo =
                fixture.AttackInfoPackets.First(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                             && value.DamageTypeWire == resolved.AttackInfoUnknown
                             && value.HitTypeWire == resolved.AttackInfoHitType
                             && value.WeaponInstance == resolved.AttackInfoWeaponInstance
                             && value.N3Unknown == resolved.AttackInfoN3Unknown
                             && resolved.CapturedDamageObservations.Contains(value.Amount));

            MessageBody[] capturedSequence =
            {
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                    weapon.PlayfieldId,
                    IdentityOf(weapon.WeaponIdentityType, weapon.WeaponIdentityInstance),
                    resolved.WeaponDefinition,
                    weapon.Energy,
                    weapon.MultipleCount),
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    IdentityOf(saw.SourceType, saw.SourceIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    IdentityOf(attack.SourceType, attack.SourceIdentity),
                    IdentityOf(attack.TargetType, attack.TargetIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                    IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                    attackInfo.Amount,
                    attackInfo.Ammo,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown)
            };

            Assert.AreEqual(4, capturedSequence.Length);
            Assert.IsInstanceOfType(capturedSequence[0], typeof(WeaponItemFullUpdateMessage));
            Assert.IsInstanceOfType(capturedSequence[1], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(capturedSequence[2], typeof(AttackMessage));
            Assert.IsInstanceOfType(capturedSequence[3], typeof(AttackInfoMessage));
            AssertHex(weapon.BodyHex, capturedSequence[0]);
            AssertHex(saw.BodyHex, capturedSequence[1]);
            AssertHex(attack.BodyHex, capturedSequence[2]);
            AssertHex(attackInfo.BodyHex, capturedSequence[3]);
        }

        [TestMethod]
        public void LooterStableProfileUsesTheCapturedSharedPacketSequence()
        {
            const string profileId = "1f9bcd8f10a573fe-3a02a8bc94c80061";
            const int runtimeSourceIdentity = unchecked((int)0x7954501B);
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Looter",
                    203745,
                    9,
                    runtimeSourceIdentity,
                    CapturedEnemyCombatContract.EquippedWeapon(
                        "active subway Looter source 0x7954501B",
                        123038,
                        123039,
                        8,
                        6),
                    out resolved,
                    out failure),
                failure);

            CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets.Single(
                value => value.OwnerIdentity == resolved.EvidenceSourceIdentity
                         && value.Energy == resolved.WeaponDefinition.InitialEnergy
                         && value.MultipleCount
                            == resolved.WeaponDefinition.SignedStatValue(
                                CharacterStat.MultipleCount));
            CapturedEnemySpecialAttackWeaponPacketFixture saw =
                fixture.SpecialAttackWeaponPackets.Single(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.Unknown5 == resolved.SpecialAttackWeaponUnknown5);
            CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.Single(
                value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
            CapturedEnemyAttackInfoPacketFixture attackInfo =
                fixture.AttackInfoPackets.First(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                             && value.DamageTypeWire == resolved.AttackInfoUnknown
                             && value.HitTypeWire == resolved.AttackInfoHitType
                             && value.WeaponInstance == resolved.AttackInfoWeaponInstance
                             && value.N3Unknown == resolved.AttackInfoN3Unknown
                             && resolved.CapturedDamageObservations.Contains(value.Amount));

            MessageBody[] capturedSequence =
            {
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                    weapon.PlayfieldId,
                    IdentityOf(weapon.WeaponIdentityType, weapon.WeaponIdentityInstance),
                    resolved.WeaponDefinition,
                    weapon.Energy,
                    weapon.MultipleCount),
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    IdentityOf(saw.SourceType, saw.SourceIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    IdentityOf(attack.SourceType, attack.SourceIdentity),
                    IdentityOf(attack.TargetType, attack.TargetIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                    IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                    attackInfo.Amount,
                    attackInfo.Ammo,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown)
            };

            Assert.AreEqual(4, capturedSequence.Length);
            Assert.IsInstanceOfType(capturedSequence[0], typeof(WeaponItemFullUpdateMessage));
            Assert.IsInstanceOfType(capturedSequence[1], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(capturedSequence[2], typeof(AttackMessage));
            Assert.IsInstanceOfType(capturedSequence[3], typeof(AttackInfoMessage));
            AssertHex(weapon.BodyHex, capturedSequence[0]);
            AssertHex(saw.BodyHex, capturedSequence[1]);
            AssertHex(attack.BodyHex, capturedSequence[2]);
            AssertHex(attackInfo.BodyHex, capturedSequence[3]);
        }

        [TestMethod]
        public void IncompleteRebuildStableProfileUsesTheCapturedSharedPacketSequence()
        {
            const string profileId = "f4b7f149cee5b2ad-b4c320f0187034b8";
            const int runtimeSourceIdentity = unchecked((int)0x79545172);
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Incomplete Rebuild",
                    203728,
                    18,
                    runtimeSourceIdentity,
                    CapturedEnemyCombatContract.EquippedWeapon(
                        "active subway Incomplete Rebuild source 0x79545172",
                        122653,
                        122654,
                        15,
                        6),
                    out resolved,
                    out failure),
                failure);

            CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets.Single(
                value => value.OwnerIdentity == resolved.EvidenceSourceIdentity
                         && value.Energy == resolved.WeaponDefinition.InitialEnergy
                         && value.MultipleCount
                            == resolved.WeaponDefinition.SignedStatValue(
                                CharacterStat.MultipleCount));
            CapturedEnemySpecialAttackWeaponPacketFixture saw =
                fixture.SpecialAttackWeaponPackets.Single(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.Unknown5 == resolved.SpecialAttackWeaponUnknown5);
            CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.Single(
                value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
            CapturedEnemyAttackInfoPacketFixture attackInfo =
                fixture.AttackInfoPackets.First(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                             && value.DamageTypeWire == resolved.AttackInfoUnknown
                             && value.HitTypeWire == resolved.AttackInfoHitType
                             && value.WeaponInstance == resolved.AttackInfoWeaponInstance
                             && value.N3Unknown == resolved.AttackInfoN3Unknown
                             && resolved.CapturedDamageObservations.Contains(value.Amount));

            MessageBody[] capturedSequence =
            {
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                    weapon.PlayfieldId,
                    IdentityOf(weapon.WeaponIdentityType, weapon.WeaponIdentityInstance),
                    resolved.WeaponDefinition,
                    weapon.Energy,
                    weapon.MultipleCount),
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    IdentityOf(saw.SourceType, saw.SourceIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    IdentityOf(attack.SourceType, attack.SourceIdentity),
                    IdentityOf(attack.TargetType, attack.TargetIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                    IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                    attackInfo.Amount,
                    attackInfo.Ammo,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown)
            };

            Assert.AreEqual(4, capturedSequence.Length);
            Assert.IsInstanceOfType(capturedSequence[0], typeof(WeaponItemFullUpdateMessage));
            Assert.IsInstanceOfType(capturedSequence[1], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(capturedSequence[2], typeof(AttackMessage));
            Assert.IsInstanceOfType(capturedSequence[3], typeof(AttackInfoMessage));
            AssertHex(weapon.BodyHex, capturedSequence[0]);
            AssertHex(saw.BodyHex, capturedSequence[1]);
            AssertHex(attack.BodyHex, capturedSequence[2]);
            AssertHex(attackInfo.BodyHex, capturedSequence[3]);
        }

        [TestMethod]
        public void FragmentedSoulStableProfileUsesTheCapturedSharedPacketSequence()
        {
            const string profileId = "41ec8ecff96a0c8c-fedb453533892b94";
            const int runtimeSourceIdentity = unchecked((int)0x79545248);
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Fragmented Soul",
                    203729,
                    18,
                    runtimeSourceIdentity,
                    CapturedEnemyCombatContract.EquippedWeapon(
                        "active subway Fragmented Soul source 0x79545248",
                        123685,
                        123686,
                        18,
                        6),
                    out resolved,
                    out failure),
                failure);

            CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets.Single(
                value => value.OwnerIdentity == resolved.EvidenceSourceIdentity
                         && value.Energy == resolved.WeaponDefinition.InitialEnergy
                         && value.MultipleCount
                            == resolved.WeaponDefinition.SignedStatValue(
                                CharacterStat.MultipleCount));
            CapturedEnemySpecialAttackWeaponPacketFixture saw =
                fixture.SpecialAttackWeaponPackets.Single(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.Unknown5 == resolved.SpecialAttackWeaponUnknown5);
            CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.Single(
                value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
            CapturedEnemyAttackInfoPacketFixture attackInfo =
                fixture.AttackInfoPackets.First(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                             && value.DamageTypeWire == resolved.AttackInfoUnknown
                             && value.HitTypeWire == resolved.AttackInfoHitType
                             && value.WeaponInstance == resolved.AttackInfoWeaponInstance
                             && value.N3Unknown == resolved.AttackInfoN3Unknown
                             && resolved.CapturedDamageObservations.Contains(value.Amount));

            MessageBody[] capturedSequence =
            {
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                    weapon.PlayfieldId,
                    IdentityOf(weapon.WeaponIdentityType, weapon.WeaponIdentityInstance),
                    resolved.WeaponDefinition,
                    weapon.Energy,
                    weapon.MultipleCount),
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    IdentityOf(saw.SourceType, saw.SourceIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    IdentityOf(attack.SourceType, attack.SourceIdentity),
                    IdentityOf(attack.TargetType, attack.TargetIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                    IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                    attackInfo.Amount,
                    attackInfo.Ammo,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown)
            };

            Assert.AreEqual(4, capturedSequence.Length);
            Assert.IsInstanceOfType(capturedSequence[0], typeof(WeaponItemFullUpdateMessage));
            Assert.IsInstanceOfType(capturedSequence[1], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(capturedSequence[2], typeof(AttackMessage));
            Assert.IsInstanceOfType(capturedSequence[3], typeof(AttackInfoMessage));
            AssertHex(weapon.BodyHex, capturedSequence[0]);
            AssertHex(saw.BodyHex, capturedSequence[1]);
            AssertHex(attack.BodyHex, capturedSequence[2]);
            AssertHex(attackInfo.BodyHex, capturedSequence[3]);
        }

        [TestMethod]
        public void RedundantScanStableProfileUsesTheCapturedSharedPacketSequence()
        {
            const string profileId = "92a71de337c6ddab-64fa18a98612853b";
            const int runtimeSourceIdentity = unchecked((int)0x795451BF);
            CapturedEnemyCombatPacketFixture fixture =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().Single(
                    value => value.ProfileId == profileId);
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    127,
                    "Redundant Scan",
                    204178,
                    19,
                    runtimeSourceIdentity,
                    CapturedEnemyCombatContract.EquippedWeapon(
                        "active subway Redundant Scan source 0x795451BF",
                        122026,
                        122027,
                        14,
                        6),
                    out resolved,
                    out failure),
                failure);

            CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets.Single(
                value => value.OwnerIdentity == resolved.EvidenceSourceIdentity
                         && value.Energy == resolved.WeaponDefinition.InitialEnergy
                         && value.MultipleCount
                            == resolved.WeaponDefinition.SignedStatValue(
                                CharacterStat.MultipleCount));
            CapturedEnemySpecialAttackWeaponPacketFixture saw =
                fixture.SpecialAttackWeaponPackets.Single(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.Unknown5 == resolved.SpecialAttackWeaponUnknown5);
            CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.Single(
                value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
            CapturedEnemyAttackInfoPacketFixture attackInfo =
                fixture.AttackInfoPackets.First(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                             && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                             && value.DamageTypeWire == resolved.AttackInfoUnknown
                             && value.HitTypeWire == resolved.AttackInfoHitType
                             && value.WeaponInstance == resolved.AttackInfoWeaponInstance
                             && value.N3Unknown == resolved.AttackInfoN3Unknown
                             && resolved.CapturedDamageObservations.Contains(value.Amount));

            MessageBody[] capturedSequence =
            {
                CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                    IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                    weapon.PlayfieldId,
                    IdentityOf(weapon.WeaponIdentityType, weapon.WeaponIdentityInstance),
                    resolved.WeaponDefinition,
                    weapon.Energy,
                    weapon.MultipleCount),
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    IdentityOf(saw.SourceType, saw.SourceIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttack(
                    IdentityOf(attack.SourceType, attack.SourceIdentity),
                    IdentityOf(attack.TargetType, attack.TargetIdentity),
                    resolved),
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                    IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                    attackInfo.Amount,
                    attackInfo.Ammo,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown)
            };

            Assert.AreEqual(4, capturedSequence.Length);
            Assert.IsInstanceOfType(capturedSequence[0], typeof(WeaponItemFullUpdateMessage));
            Assert.IsInstanceOfType(capturedSequence[1], typeof(SpecialAttackWeaponMessage));
            Assert.IsInstanceOfType(capturedSequence[2], typeof(AttackMessage));
            Assert.IsInstanceOfType(capturedSequence[3], typeof(AttackInfoMessage));
            AssertHex(weapon.BodyHex, capturedSequence[0]);
            AssertHex(saw.BodyHex, capturedSequence[1]);
            AssertHex(attack.BodyHex, capturedSequence[2]);
            AssertHex(attackInfo.BodyHex, capturedSequence[3]);
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
        public void MarcusCapturedZeroEnergyAndAmmoRemainStableWhileAmbiguousSawStateStaysQuarantined()
        {
            const int marcusSourceIdentity = unchecked((int)0x78E0FC62);
            CapturedEnemyCombatProfileDefinition marcus =
                CapturedEnemyCombatProfileCatalog.GetProfilesForTests().Single(
                    value => value.ResourceId == 6553
                             && value.Name == "Marcus Stone"
                             && value.MonsterData == 258744
                             && value.Level == 15
                             && value.ContainsSource(marcusSourceIdentity));
            CapturedEnemyCombatProfileStreamDefinition stream = marcus.Streams.Single(
                value => value.DamageTypeWire == 0);

            Assert.IsNotNull(marcus.WeaponDefinition);
            Assert.AreEqual(0, marcus.WeaponDefinition.InitialEnergy);
            Assert.AreEqual(0, stream.InitialAmmoCount);
            Assert.IsTrue(marcus.CaptureEvidenceSafe);
            Assert.IsFalse(marcus.DeterministicRuntimeInitializationProven);
            Assert.IsFalse(marcus.CaptureRuntimeEvidenceSafe);

            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsFalse(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    marcus.ResourceId,
                    marcus.Name,
                    marcus.MonsterData,
                    marcus.Level,
                    marcusSourceIdentity,
                    CapturedEnemyCombatContract.Unresolved(
                        "Marcus mutable-state quarantine test",
                        true),
                    out resolved,
                    out failure));
            StringAssert.Contains(failure, "explicitly unsafe for runtime replay");
            Assert.IsFalse(resolved.IsCombatReady);

            AttackInfoMessage attackInfo = CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                SimpleChar(marcusSourceIdentity),
                SimpleChar(LocalPlayerIdentity),
                stream.MinimumObservedDamage,
                0,
                stream.WeaponSlot,
                stream.DamageTypeWire,
                stream.HitTypeWire,
                stream.WeaponInstance,
                stream.N3Unknown);
            Assert.AreEqual(0, attackInfo.Unknown2);

            string runtime = File.ReadAllText(
                Path.Combine(
                    FindRepositoryRoot(),
                    "AORebirth",
                    "Server",
                    "ZoneEngine",
                    "Core",
                    "Playfields",
                    "CapturedEnemyCombatContract.cs"));
            Assert.IsTrue(runtime.Contains("if (energy == 0)"));
            Assert.IsFalse(runtime.Contains("currentEnergy != -1 && currentEnergy <= 0"));
        }

        [TestMethod]
        public void Level48DeathlessUsesCalculatedDamageWithExactArchetypePacketSemantics()
        {
            CapturedEnemyCombatContract resolved;
            string failure;
            Assert.IsTrue(
                CapturedEnemyCombatProfileCatalog.TryResolve(
                    1931,
                    "Deathless Legionnaire",
                    42981,
                    48,
                    unchecked((int)0x7987F61A),
                    CapturedEnemyCombatContract.Unresolved(
                        "level 48 Deathless packet semantics",
                        true),
                    out resolved,
                    out failure),
                failure);

            Assert.IsTrue(resolved.IsCombatReady);
            Assert.IsTrue(resolved.UsesCaptureProvenArchetype);
            Assert.IsTrue(resolved.UsesEquippedWeaponDamage);
            Assert.IsTrue(resolved.UsesEquippedWeaponTiming);
            Identity attacker = SimpleChar(unchecked((int)0x7987F61A));
            Identity target = SimpleChar(LocalPlayerIdentity);
            SpecialAttackWeaponMessage specialAttackWeapon =
                CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                    attacker,
                    resolved);
            AttackMessage attack = CapturedEnemyCombatPacketFactory.CreateAttack(
                attacker,
                target,
                resolved);
            const int productionCalculatedDamage = 35;
            AttackInfoMessage attackInfo =
                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                    attacker,
                    target,
                    productionCalculatedDamage,
                    resolved.AttackInfoAmmoCount,
                    resolved.AttackInfoWeaponSlot,
                    resolved.AttackInfoUnknown,
                    resolved.AttackInfoHitType,
                    resolved.AttackInfoWeaponInstance,
                    resolved.AttackInfoN3Unknown);

            AssertCapturedOrder(new MessageBody[] { specialAttackWeapon, attack, attackInfo });
            Assert.AreEqual(0, specialAttackWeapon.Specials.Length);
            Assert.AreEqual(0, specialAttackWeapon.Unknown);
            Assert.AreEqual(0, specialAttackWeapon.Unknown5);
            Assert.AreEqual(0, attack.Unknown);
            Assert.AreEqual((byte)0, attack.Action);
            Assert.AreEqual(target, attack.Target);
            Assert.AreEqual(productionCalculatedDamage, attackInfo.Unknown1);
            Assert.AreNotEqual(41, attackInfo.Unknown1);
            Assert.AreNotEqual(42, attackInfo.Unknown1);
            Assert.AreEqual(-1, attackInfo.Unknown2);
            Assert.AreEqual(6, attackInfo.Unknown3);
            Assert.AreEqual(0, attackInfo.Unknown4);
            Assert.AreEqual(3, attackInfo.Unknown5);
            Assert.AreEqual(0, attackInfo.Unknown6);
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

            Assert.AreEqual(167, spawns.Length);
            Assert.AreEqual(14, ready.Count);
            Assert.AreEqual(153, quarantined.Count);
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
            Assert.AreEqual(167, templeSpawns.Length);
            Assert.AreEqual(14, templeContracts.Count(value => value.IsCombatReady));
            Assert.AreEqual(153, templeContracts.Count(value => value.IsQuarantined));

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
            Assert.IsTrue(visibility.Contains("item.MultipleCount"));
            Assert.IsFalse(coordinator.Contains("SendIncomingHitChatIfPlayer"));
            Assert.IsFalse(coordinator.Contains("hit you for"));
            Assert.IsFalse(templeCatalog.Contains("new AttackInfoMessage"));
            Assert.IsFalse(templeCatalog.Contains("new AttackMessage"));
            Assert.IsFalse(templeCatalog.Contains("new SpecialAttackWeaponMessage"));
            Assert.IsTrue(marcus.Contains("CapturedEnemyCombatContract.CapturedSpecialSequence("));
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
            Assert.IsTrue(contractRuntime.Contains("if (energy == 0)"));
            Assert.IsTrue(contractRuntime.Contains("if (currentEnergy < -1"));
            Assert.IsFalse(contractRuntime.Contains("currentEnergy != -1 && currentEnergy <= 0"));
            Assert.IsTrue(contractRuntime.Contains("captured weapon Energy is exhausted"));
            Assert.IsTrue(functionHit.Contains("CapturedEnemyCombatFunctionHitQuarantined"));
            Assert.IsTrue(npcRuntime.Contains("Captured enemy taunt refused"));
            Assert.IsTrue(npcRuntime.Contains("!NpcAiProfiles.CanRetaliate(npcController.AiProfile)"));
            Assert.IsTrue(otherImplementedHostileEntryPoints.All(
                path => File.ReadAllText(path).Contains("CapturedEnemyCombatRuntime.Prepare")));
            Assert.AreEqual(3, sourceOwnedWeaponCallers.Length);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "CapturedEnemyCombatContract.cs",
                    "CapturedEnemyCombatProfileCatalog.cs",
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

        private static Identity IdentityOf(int type, int instance)
        {
            return new Identity { Type = (IdentityType)type, Instance = instance };
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
