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

    using ZoneEngine.Core;
    using ZoneEngine.Core.Playfields;

    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    [TestClass]
    public class CapturedEnemyCombatGeneratedPacketFixtureTests
    {
        [TestMethod]
        public void EveryGeneratedProfileReproducesItsRawCapturedPacketBodies()
        {
            Dictionary<string, CapturedEnemyCombatProfileDefinition> profiles =
                CapturedEnemyCombatProfileCatalog.GetProfilesForTests().ToDictionary(
                    value => value.ProfileId,
                    StringComparer.Ordinal);
            CapturedEnemyCombatPacketFixture[] fixtures =
                CapturedEnemyCombatGeneratedPacketFixtures.Create();

            Assert.IsTrue(fixtures.Length > 0);
            Assert.AreEqual(profiles.Count, fixtures.Length);
            Assert.AreEqual(
                fixtures.Length,
                fixtures.Select(value => value.ProfileId).Distinct(StringComparer.Ordinal).Count());

            foreach (CapturedEnemyCombatPacketFixture fixture in fixtures)
            {
                CapturedEnemyCombatProfileDefinition profile = profiles[fixture.ProfileId];
                AssertUniquePacketIds(
                    fixture.WeaponPackets.Select(value => value.PacketId),
                    fixture.ProfileId + " WeaponItemFullUpdate packet ids");
                AssertUniquePacketIds(
                    fixture.SpecialAttackWeaponPackets.Select(value => value.PacketId),
                    fixture.ProfileId + " SpecialAttackWeapon packet ids");
                AssertUniquePacketIds(
                    fixture.AttackPackets.Select(value => value.PacketId),
                    fixture.ProfileId + " Attack packet ids");
                AssertUniquePacketIds(
                    fixture.AttackInfoPackets.Select(value => value.PacketId),
                    fixture.ProfileId + " AttackInfo packet ids");

                foreach (CapturedEnemyWeaponPacketFixture weapon in fixture.WeaponPackets)
                {
                    Assert.IsNotNull(profile.WeaponDefinition, weapon.PacketId);
                    AssertHex(
                        weapon.BodyHex,
                        CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                            IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                            weapon.PlayfieldId,
                            IdentityOf(
                                weapon.WeaponIdentityType,
                                weapon.WeaponIdentityInstance),
                            profile.WeaponDefinition,
                            weapon.Energy,
                            weapon.MultipleCount),
                        weapon.PacketId);
                }

                foreach (CapturedEnemySpecialAttackWeaponPacketFixture specialAttackWeapon
                         in fixture.SpecialAttackWeaponPackets)
                {
                    AssertHex(
                        specialAttackWeapon.BodyHex,
                        CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                            IdentityOf(
                                specialAttackWeapon.SourceType,
                                specialAttackWeapon.SourceIdentity),
                            profile.SpecialAttacks,
                            profile.SpecialAttackWeaponN3Unknown,
                            profile.SpecialAttackWeaponUnknown1,
                            profile.SpecialAttackWeaponUnknown2,
                            profile.SpecialAttackWeaponUnknown3,
                            profile.SpecialAttackWeaponUnknown4,
                            specialAttackWeapon.Unknown5),
                        specialAttackWeapon.PacketId);
                }

                foreach (CapturedEnemyAttackPacketFixture attack in fixture.AttackPackets)
                {
                    AssertHex(
                        attack.BodyHex,
                        CapturedEnemyCombatPacketFactory.CreateAttack(
                            IdentityOf(attack.SourceType, attack.SourceIdentity),
                            IdentityOf(attack.TargetType, attack.TargetIdentity),
                            profile.AttackN3Unknown,
                            profile.AttackAction),
                        attack.PacketId);
                }

                foreach (CapturedEnemyAttackInfoPacketFixture attackInfo in fixture.AttackInfoPackets)
                {
                    CapturedEnemyCombatProfileStreamDefinition stream = profile.Streams.FirstOrDefault(
                        value => value.WeaponSlot == attackInfo.WeaponSlot
                                 && value.DamageTypeWire == attackInfo.DamageTypeWire
                                 && value.HitTypeWire == attackInfo.HitTypeWire
                                 && value.WeaponInstance == attackInfo.WeaponInstance
                                 && value.N3Unknown == attackInfo.N3Unknown
                                 && value.CapturedDamageObservations.Contains(attackInfo.Amount));
                    Assert.IsNotNull(stream, attackInfo.PacketId);
                    AssertHex(
                        attackInfo.BodyHex,
                        CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                            IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                            IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                            attackInfo.Amount,
                            attackInfo.Ammo,
                            stream.WeaponSlot,
                            stream.DamageTypeWire,
                            stream.HitTypeWire,
                            stream.WeaponInstance,
                            stream.N3Unknown),
                        attackInfo.PacketId);
                }
            }
        }

        [TestMethod]
        public void Md26137CapturedLevelsReproduceTheirExactFourPacketSequences()
        {
            string[] profileIds =
            {
                "8dae5024f999475e-03dc0b29328f8462",
                "58e682ec3ebb63c8-b677b63db5c16f15",
                "a84eedad1a598b40-c4f21242a4a7ba96",
                "e30508b3b9b8e352-a87351572a2f5f23",
                "3ec94c5698a809ab-d38e253d2479c92e",
                "2ee43d964a95a575-9ca73d7846c38f6c"
            };
            Dictionary<string, CapturedEnemyCombatProfileDefinition> profiles =
                CapturedEnemyCombatProfileCatalog.GetProfilesForTests().ToDictionary(
                    value => value.ProfileId,
                    StringComparer.Ordinal);
            Dictionary<string, CapturedEnemyCombatPacketFixture> fixtures =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().ToDictionary(
                    value => value.ProfileId,
                    StringComparer.Ordinal);

            foreach (string profileId in profileIds)
            {
                CapturedEnemyCombatProfileDefinition profile = profiles[profileId];
                CapturedEnemyCombatPacketFixture fixture = fixtures[profileId];
                CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets[0];
                CapturedEnemySpecialAttackWeaponPacketFixture saw =
                    fixture.SpecialAttackWeaponPackets.First(
                        value => value.SourceIdentity == weapon.OwnerIdentity
                                 && value.Unknown5
                                    == profile.SpecialAttackWeaponUnknown5);
                CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.First(
                    value => value.SourceIdentity == saw.SourceIdentity);
                CapturedEnemyAttackInfoPacketFixture attackInfo =
                    fixture.AttackInfoPackets.First(
                        value => value.SourceIdentity == saw.SourceIdentity
                                 && value.WeaponSlot == profile.Streams[0].WeaponSlot
                                 && value.DamageTypeWire
                                    == profile.Streams[0].DamageTypeWire
                                 && value.HitTypeWire == profile.Streams[0].HitTypeWire
                                 && value.WeaponInstance
                                    == profile.Streams[0].WeaponInstance
                                 && value.N3Unknown == profile.Streams[0].N3Unknown);
                MessageBody[] sequence =
                {
                    CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                        IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                        weapon.PlayfieldId,
                        IdentityOf(
                            weapon.WeaponIdentityType,
                            weapon.WeaponIdentityInstance),
                        profile.WeaponDefinition,
                        weapon.Energy,
                        weapon.MultipleCount),
                    CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                        IdentityOf(saw.SourceType, saw.SourceIdentity),
                        profile.SpecialAttacks,
                        profile.SpecialAttackWeaponN3Unknown,
                        profile.SpecialAttackWeaponUnknown1,
                        profile.SpecialAttackWeaponUnknown2,
                        profile.SpecialAttackWeaponUnknown3,
                        profile.SpecialAttackWeaponUnknown4,
                        saw.Unknown5),
                    CapturedEnemyCombatPacketFactory.CreateAttack(
                        IdentityOf(attack.SourceType, attack.SourceIdentity),
                        IdentityOf(attack.TargetType, attack.TargetIdentity),
                        profile.AttackN3Unknown,
                        profile.AttackAction),
                    CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                        IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                        IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                        attackInfo.Amount,
                        attackInfo.Ammo,
                        profile.Streams[0].WeaponSlot,
                        profile.Streams[0].DamageTypeWire,
                        profile.Streams[0].HitTypeWire,
                        profile.Streams[0].WeaponInstance,
                        profile.Streams[0].N3Unknown)
                };

                Assert.AreEqual(4, sequence.Length, profileId);
                Assert.IsInstanceOfType(
                    sequence[0],
                    typeof(WeaponItemFullUpdateMessage),
                    profileId);
                Assert.IsInstanceOfType(
                    sequence[1],
                    typeof(SpecialAttackWeaponMessage),
                    profileId);
                Assert.IsInstanceOfType(sequence[2], typeof(AttackMessage), profileId);
                Assert.IsInstanceOfType(
                    sequence[3],
                    typeof(AttackInfoMessage),
                    profileId);
                AssertHex(weapon.BodyHex, sequence[0], weapon.PacketId);
                AssertHex(saw.BodyHex, sequence[1], saw.PacketId);
                AssertHex(attack.BodyHex, sequence[2], attack.PacketId);
                AssertHex(attackInfo.BodyHex, sequence[3], attackInfo.PacketId);

                SpecialAttackWeaponMessage mutableState =
                    CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                        IdentityOf(saw.SourceType, saw.SourceIdentity),
                        profile.SpecialAttacks,
                        profile.SpecialAttackWeaponN3Unknown,
                        profile.SpecialAttackWeaponUnknown1,
                        profile.SpecialAttackWeaponUnknown2,
                        profile.SpecialAttackWeaponUnknown3,
                        profile.SpecialAttackWeaponUnknown4,
                        99);
                Assert.AreEqual(profile.SpecialAttackWeaponUnknown1, mutableState.Unknown1);
                Assert.AreEqual(profile.SpecialAttackWeaponUnknown2, mutableState.Unknown2);
                Assert.AreEqual(profile.SpecialAttackWeaponUnknown3, mutableState.Unknown3);
                Assert.AreEqual(profile.SpecialAttackWeaponUnknown4, mutableState.Unknown4);
                Assert.AreEqual(99, mutableState.Unknown5);
                Assert.AreEqual(0, mutableState.Specials.Length);
            }
        }

        [TestMethod]
        public void EveryRuntimeResolvableProfileReplaysCapturedPacketsThroughTheSharedCatalog()
        {
            Dictionary<string, CapturedEnemyCombatPacketFixture> fixtures =
                CapturedEnemyCombatGeneratedPacketFixtures.Create().ToDictionary(
                    value => value.ProfileId,
                    StringComparer.Ordinal);
            int resolvedCount = 0;
            foreach (CapturedEnemyCombatProfileDefinition profile
                     in CapturedEnemyCombatProfileCatalog.GetProfilesForTests())
            {
                CapturedEnemyCombatContract baseline =
                    CapturedEnemyCombatContract.Unresolved(
                        "resolved exact-byte fixture baseline",
                        true);
                if (profile.ResourceId == 127
                    && ((profile.Name == "Filth Flea"
                         && profile.MonsterData == 17657
                         && profile.Level == 5)
                        || (profile.Name == "Disobedient Bot"
                            && profile.MonsterData == 17649
                            && profile.Level == 8)))
                {
                    baseline = CapturedSubwayCombatCatalog.For(
                        profile.Name,
                        profile.MonsterData,
                        profile.Level);
                }

                CapturedEnemyCombatContract resolved;
                string failure;
                if (!CapturedEnemyCombatProfileCatalog.TryResolve(
                        profile.ResourceId,
                        profile.Name,
                        profile.MonsterData,
                        profile.Level,
                        profile.RepresentativeEvidenceSourceIdentity,
                        baseline,
                        out resolved,
                        out failure))
                {
                    continue;
                }

                resolvedCount++;
                bool mathematicalArchetype =
                    resolved.UsesCaptureProvenArchetype
                    && resolved.CaptureProvenArchetypeId.StartsWith(
                        "formula=",
                        StringComparison.Ordinal);
                CapturedEnemyCombatProfileDefinition selectedProfile =
                    CapturedEnemyCombatProfileCatalog.GetProfilesForTests().SingleOrDefault(
                        value => (mathematicalArchetype
                                      ? value.MatchesArchetypeKey(
                                          profile.ResourceId,
                                          profile.Name,
                                          profile.MonsterData)
                                      : value.MatchesKey(
                                          profile.ResourceId,
                                          profile.Name,
                                          profile.MonsterData,
                                          profile.Level))
                                 && (value.CaptureRuntimeEvidenceSafe
                                     || (resolved.AttackModel
                                         == CapturedEnemyAttackModel.Specialized
                                         && value.CaptureEvidenceSafe
                                         && (!resolved.UsesProductionSpecializedValues
                                             || value.MatchesSpecialized(resolved))))
                                 && (mathematicalArchetype
                                     || value.Evidence == resolved.Evidence)
                                 && value.ContainsSource(resolved.EvidenceSourceIdentity));
                Assert.IsNotNull(
                    selectedProfile,
                    profile.ProfileId + " resolved without a matching capture fixture profile; evidence source="
                    + resolved.EvidenceSourceIdentity);
                CapturedEnemyCombatPacketFixture fixture = fixtures[selectedProfile.ProfileId];
                CapturedEnemySpecialAttackWeaponPacketFixture saw =
                    fixture.SpecialAttackWeaponPackets.FirstOrDefault(
                        value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                                 && value.Unknown5
                                    == resolved.SpecialAttackWeaponUnknown5);
                Assert.IsNotNull(saw, selectedProfile.ProfileId + " resolved SAW evidence");
                Identity source = IdentityOf(saw.SourceType, saw.SourceIdentity);
                AssertHex(
                    saw.BodyHex,
                    CapturedEnemyCombatPacketFactory.CreateSpecialAttackWeapon(
                        source,
                        resolved),
                    saw.PacketId + " resolved catalog SAW");

                CapturedEnemyAttackPacketFixture attack = fixture.AttackPackets.FirstOrDefault(
                    value => value.SourceIdentity == resolved.EvidenceSourceIdentity);
                Assert.IsNotNull(attack, selectedProfile.ProfileId + " resolved Attack evidence");
                AssertHex(
                    attack.BodyHex,
                    CapturedEnemyCombatPacketFactory.CreateAttack(
                        IdentityOf(attack.SourceType, attack.SourceIdentity),
                        IdentityOf(attack.TargetType, attack.TargetIdentity),
                        resolved),
                    attack.PacketId + " resolved catalog Attack");

                if (resolved.WeaponDefinition != null)
                {
                    int capturedMultipleCount = resolved.WeaponDefinition.SignedStatValue(
                        CharacterStat.MultipleCount);
                    CapturedEnemyWeaponPacketFixture weapon = fixture.WeaponPackets.FirstOrDefault(
                        value => value.OwnerIdentity == resolved.EvidenceSourceIdentity
                                 && value.Energy
                                    == resolved.WeaponDefinition.InitialEnergy
                                 && value.MultipleCount == capturedMultipleCount);
                    Assert.IsNotNull(weapon, selectedProfile.ProfileId + " resolved WIFU evidence");
                    AssertHex(
                        weapon.BodyHex,
                        CapturedEnemyCombatPacketFactory.CreateWeaponDefinition(
                            IdentityOf(weapon.OwnerType, weapon.OwnerIdentity),
                            weapon.PlayfieldId,
                            IdentityOf(
                                weapon.WeaponIdentityType,
                                weapon.WeaponIdentityInstance),
                            resolved.WeaponDefinition,
                            weapon.Energy,
                            weapon.MultipleCount),
                        weapon.PacketId + " resolved catalog WIFU");
                }

                if (resolved.AttackModel == CapturedEnemyAttackModel.FixedAttackInfo)
                {
                    CapturedEnemyAttackInfoPacketFixture attackInfo =
                        fixture.AttackInfoPackets.FirstOrDefault(
                            value => value.SourceIdentity == resolved.EvidenceSourceIdentity
                                     && value.WeaponSlot == resolved.AttackInfoWeaponSlot
                                     && value.DamageTypeWire == resolved.AttackInfoUnknown
                                     && value.HitTypeWire == resolved.AttackInfoHitType
                                     && value.WeaponInstance
                                        == resolved.AttackInfoWeaponInstance
                                     && value.N3Unknown == resolved.AttackInfoN3Unknown
                                     && resolved.CapturedDamageObservations.Contains(
                                         value.Amount));
                    Assert.IsNotNull(
                        attackInfo,
                        selectedProfile.ProfileId + " resolved AttackInfo evidence");
                    AssertHex(
                        attackInfo.BodyHex,
                        CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                            IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                            IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                            attackInfo.Amount,
                            attackInfo.Ammo,
                            resolved.AttackInfoWeaponSlot,
                            resolved.AttackInfoUnknown,
                            resolved.AttackInfoHitType,
                            resolved.AttackInfoWeaponInstance,
                            resolved.AttackInfoN3Unknown),
                        attackInfo.PacketId + " resolved catalog AttackInfo");
                    Assert.AreEqual(
                        resolved.AttackInfoAmmoCount,
                        CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                            IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                            IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                            attackInfo.Amount,
                            resolved.AttackInfoAmmoCount,
                            resolved.AttackInfoWeaponSlot,
                            resolved.AttackInfoUnknown,
                            resolved.AttackInfoHitType,
                            resolved.AttackInfoWeaponInstance,
                            resolved.AttackInfoN3Unknown).Unknown2,
                        profile.ProfileId + " resolved mutable AttackInfo ammunition");
                }
                else if (resolved.AttackModel == CapturedEnemyAttackModel.Specialized)
                {
                    CapturedEnemyCombatAttackDefinition[] resolvedAttacks =
                        GetSpecializedAttacks(resolved).ToArray();
                    Assert.IsTrue(
                        resolvedAttacks.Length > 0,
                        profile.ProfileId + " resolved specialized attacks");
                    foreach (CapturedEnemyCombatAttackDefinition resolvedAttack
                             in resolvedAttacks)
                    {
                        CapturedEnemyAttackInfoPacketFixture attackInfo =
                            fixture.AttackInfoPackets.FirstOrDefault(
                                value => value.SourceIdentity
                                         == resolved.EvidenceSourceIdentity
                                         && value.WeaponSlot
                                            == resolvedAttack.AttackInfoWeaponSlot
                                         && value.DamageTypeWire
                                            == resolvedAttack.AttackInfoUnknown
                                         && value.HitTypeWire
                                            == resolvedAttack.AttackInfoHitType
                                         && value.WeaponInstance
                                            == resolvedAttack.AttackInfoWeaponInstance
                                         && value.N3Unknown
                                            == resolvedAttack.AttackInfoN3Unknown
                                         && (mathematicalArchetype
                                                 ? value.Amount
                                                   >= resolvedAttack.MinDamage
                                                   && value.Amount
                                                      <= resolvedAttack.MaxDamage
                                                 : resolvedAttack
                                                     .CapturedDamageObservations
                                                     .Contains(value.Amount)));
                        Assert.IsNotNull(
                            attackInfo,
                            profile.ProfileId + " resolved specialized AttackInfo evidence");
                        AssertHex(
                            attackInfo.BodyHex,
                            CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                                IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                                IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                                attackInfo.Amount,
                                attackInfo.Ammo,
                                resolvedAttack.AttackInfoWeaponSlot,
                                resolvedAttack.AttackInfoUnknown,
                                resolvedAttack.AttackInfoHitType,
                                resolvedAttack.AttackInfoWeaponInstance,
                                resolvedAttack.AttackInfoN3Unknown),
                            attackInfo.PacketId
                            + " resolved specialized catalog AttackInfo");
                        Assert.AreEqual(
                            resolvedAttack.AttackInfoAmmoCount,
                            CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                                IdentityOf(attackInfo.SourceType, attackInfo.SourceIdentity),
                                IdentityOf(attackInfo.TargetType, attackInfo.TargetIdentity),
                                attackInfo.Amount,
                                resolvedAttack.AttackInfoAmmoCount,
                                resolvedAttack.AttackInfoWeaponSlot,
                                resolvedAttack.AttackInfoUnknown,
                                resolvedAttack.AttackInfoHitType,
                                resolvedAttack.AttackInfoWeaponInstance,
                                resolvedAttack.AttackInfoN3Unknown).Unknown2,
                            profile.ProfileId
                            + " resolved specialized mutable AttackInfo ammunition");
                        if (resolvedAttack.LethalAttackInfoUnknown.HasValue)
                        {
                            CapturedEnemyAttackInfoPacketFixture lethalAttackInfo =
                                fixture.AttackInfoPackets.Single(
                                    value => value.SourceIdentity
                                             == resolved.EvidenceSourceIdentity
                                             && value.WeaponSlot
                                                == resolvedAttack.AttackInfoWeaponSlot
                                             && value.DamageTypeWire
                                                == resolvedAttack.LethalAttackInfoUnknown.Value
                                             && value.HitTypeWire
                                                == resolvedAttack.AttackInfoHitType
                                             && value.WeaponInstance
                                                == resolvedAttack.AttackInfoWeaponInstance
                                             && value.N3Unknown
                                                == resolvedAttack.AttackInfoN3Unknown);
                            AssertHex(
                                lethalAttackInfo.BodyHex,
                                CapturedEnemyCombatPacketFactory.CreateAttackInfo(
                                    IdentityOf(
                                        lethalAttackInfo.SourceType,
                                        lethalAttackInfo.SourceIdentity),
                                    IdentityOf(
                                        lethalAttackInfo.TargetType,
                                        lethalAttackInfo.TargetIdentity),
                                    lethalAttackInfo.Amount,
                                    lethalAttackInfo.Ammo,
                                    resolvedAttack.AttackInfoWeaponSlot,
                                    resolvedAttack.LethalAttackInfoUnknown.Value,
                                    resolvedAttack.AttackInfoHitType,
                                    resolvedAttack.AttackInfoWeaponInstance,
                                    resolvedAttack.AttackInfoN3Unknown),
                                lethalAttackInfo.PacketId
                                + " resolved specialized lethal AttackInfo");
                        }
                    }
                }
            }

            Assert.IsTrue(resolvedCount > 15, "The shared catalog must resolve beyond the seed set.");
        }

        private static IEnumerable<CapturedEnemyCombatAttackDefinition> GetSpecializedAttacks(
            CapturedEnemyCombatContract contract)
        {
            if (contract.SpecialAttackSequence != null)
            {
                if (contract.SpecialAttackSequence.OpeningAttack != null)
                {
                    yield return contract.SpecialAttackSequence.OpeningAttack;
                }

                yield return contract.SpecialAttackSequence.RepeatingAttack;
                yield break;
            }

            if (contract.ParallelAttackSequence == null)
            {
                yield break;
            }

            foreach (CapturedEnemyParallelAttackStreamDefinition stream
                     in contract.ParallelAttackSequence.Streams)
            {
                yield return stream.Attack;
            }
        }

        private static void AssertUniquePacketIds(IEnumerable<string> packetIds, string evidence)
        {
            string[] values = packetIds.ToArray();
            Assert.AreEqual(
                values.Length,
                values.Where(value => !string.IsNullOrEmpty(value))
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                evidence);
        }

        private static Identity IdentityOf(int type, int instance)
        {
            return new Identity { Type = (IdentityType)type, Instance = instance };
        }

        private static void AssertHex(string expected, MessageBody message, string evidence)
        {
            Assert.AreEqual(
                expected,
                BitConverter.ToString(Serialize(message)).Replace("-", string.Empty),
                evidence);
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
