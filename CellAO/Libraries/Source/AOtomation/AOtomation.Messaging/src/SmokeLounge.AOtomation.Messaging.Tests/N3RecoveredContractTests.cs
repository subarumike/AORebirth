namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using StreamReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    [TestClass]
    public class N3RecoveredContractTests
    {
        [TestMethod]
        public void RecoveredEnumKeysMatchCurrentClientStripdown()
        {
            Assert.AreEqual(0x36510078, (int)N3MessageType.ToClientQuit);
            Assert.AreEqual(0x36510078, (int)N3MessageType.Despawn);
            Assert.AreEqual(0x47483633, (int)N3MessageType.DropDynel);
            Assert.AreEqual(0x264B514B, (int)N3MessageType.RelocateDynels);
            Assert.AreEqual(0x4C530320, (int)N3MessageType.LocalityUpdate);
            Assert.AreEqual(0x1F4D5F7E, (int)N3MessageType.ClientContainerAddItem);
            Assert.AreEqual(0x37136C6B, (int)N3MessageType.ClientGetItem);

            Assert.AreEqual(0x4D2A313B, (int)N3MessageType.TeamInvite);
            Assert.AreEqual(0x45072A2D, (int)N3MessageType.UpdateClientVisual);

            Assert.AreEqual(0x28251F01, (int)N3MessageType.StartLogout);
            Assert.AreEqual(0x56353038, (int)N3MessageType.StopLogout);
            Assert.AreEqual(0x445F2A0B, (int)N3MessageType.Resurrect);
            Assert.AreEqual(0x54111123, (int)N3MessageType.CharDCMove);
            Assert.AreEqual(0x28494070, (int)N3MessageType.Attack);
            Assert.AreEqual(0x46002F16, (int)N3MessageType.AttackInfo);
            Assert.AreEqual(0x3710256C, (int)N3MessageType.HealthDamage);
            Assert.AreEqual(0x485E7202, (int)N3MessageType.InventoryUpdated);
            Assert.AreEqual(0x47537A24, (int)N3MessageType.ContainerAddItem);
            Assert.AreEqual(0x4A41203E, (int)N3MessageType.StopFight);
            Assert.AreEqual(0x5469373F, (int)N3MessageType.ClientMoveItemToInventory);
            Assert.AreEqual(0x5C654B28, (int)N3MessageType.MissedAttackInfo);
            Assert.AreEqual(0x754F1115, (int)N3MessageType.SpecialAttackInfo);
            Assert.AreEqual(0x260F3671, (int)N3MessageType.FollowTarget);
            Assert.AreEqual(0x60201D0E, (int)N3MessageType.SetWantedDirection);
        }

        [TestMethod]
        public void RecoveredCustomPacketSizesMatchCurrentClientStripdown()
        {
            Identity identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 1234 };
            Identity item = new Identity { Type = IdentityType.WeaponInstance, Instance = 5678 };

            AssertSerializedSize(new ToClientQuitMessage(), 4);
            AssertSerializedSize(new DespawnMessage { Identity = identity, Unknown = 1 }, 13);
            AssertSerializedSize(
                new DropDynelMessage
                    {
                        Identity = identity,
                        Position = new Vector3 { X = 1.0f, Y = 2.0f, Z = 3.0f }
                    },
                24);
            AssertSerializedSize(
                new RelocateDynelsMessage
                    {
                        Identity = identity,
                        RelocatedIdentities = new[] { identity, item }
                    },
                32);
            AssertSerializedSize(
                new LocalityUpdateMessage
                    {
                        Position = new Vector3 { X = 10.0f, Y = 20.0f, Z = 30.0f },
                        LocalityFlag = 1
                    },
                17);
            AssertSerializedSize(
                new ClientContainerAddItemMessage { Identity1 = identity, Identity2 = item },
                20);
            AssertSerializedSize(new ClientGetItemMessage { Identity1 = item }, 12);

            AssertSerializedSize(new StartLogoutMessage { Identity = identity }, 12);
            AssertSerializedSize(new StopLogoutMessage { Identity = identity }, 12);
            AssertSerializedSize(new ResurrectMessage { Unknown1 = 1, Unknown2 = 2 }, 12);
        }

        [TestMethod]
        public void CapturedCombatAndInventoryPacketsUseStandardN3Envelope()
        {
            Identity actor = new Identity { Type = IdentityType.CanbeAffected, Instance = 0x12 };
            Identity target = new Identity { Type = IdentityType.CanbeAffected, Instance = 0x0F4241 };
            Identity missedTarget = new Identity { Type = IdentityType.CanbeAffected, Instance = unchecked((int)0x7888E56E) };
            Identity specialTarget = new Identity { Type = IdentityType.CanbeAffected, Instance = unchecked((int)0x782BC546) };
            Identity sourceSlot = new Identity { Type = (IdentityType)0x6B, Instance = 0x007C0000 };

            var attack = new AttackMessage
                         {
                             Identity = actor,
                             Unknown = 1,
                             Target = target,
                             Action = 0
                         };
            CollectionAssert.AreEqual(
                HexToBytes("284940700000C35000000012010000C350000F424100"),
                Serialize(attack));

            var attackInfo = new AttackInfoMessage
                             {
                                 Identity = actor,
                                 Unknown = 1,
                                 Unknown1 = 0x0F,
                                 Unknown2 = 0x28,
                                 Unknown3 = 0x08,
                                 Target = target,
                                 Unknown4 = 4,
                                 Unknown5 = 3,
                                 Unknown6 = 0
                             };
            CollectionAssert.AreEqual(
                HexToBytes("46002F160000C35000000012010000000F00000028000000080000C350000F4241000000040000000300000000"),
                Serialize(attackInfo));

            var stopFight = new StopFightMessage
                            {
                                Identity = actor,
                                Unknown = 1,
                                Unknown1 = 1
                            };
            CollectionAssert.AreEqual(
                HexToBytes("4A41203E0000C350000000120100000001"),
                Serialize(stopFight));

            var healthDamage = new HealthDamageMessage
                               {
                                   Identity = target,
                                   Unknown = 1,
                                   Unknown1 = 5,
                                   Unknown2 = 0x0F,
                                   Unknown3 = 0x1B,
                                   Unknown4 = 0,
                                   Target = actor,
                                   Unknown5 = 0
                               };
            CollectionAssert.AreEqual(
                HexToBytes("3710256C0000C350000F424101000000050000000F0000001B000000000000C3500000001200000000"),
                Serialize(healthDamage));

            var missed = new MissedAttackInfoMessage
                         {
                             Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = unchecked((int)0x77E458F5) },
                             Unknown = 1,
                             Unknown1 = -1,
                             Unknown2 = 6,
                             Unknown3 = missedTarget,
                             Unknown4 = new Identity { Type = IdentityType.CanbeAffected, Instance = unchecked((int)0x77E458F5) },
                             Unknown5 = 0
                         };
            CollectionAssert.AreEqual(
                HexToBytes("5C654B280000C35077E458F501FFFFFFFF000000060000C3507888E56E0000C35077E458F500000000"),
                Serialize(missed));

            var special = new SpecialAttackInfo
                          {
                              Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = unchecked((int)0x3CAC6F14) },
                              Unknown = 0,
                              Unknown1 = 6,
                              Unknown2 = 0x04E1,
                              Unknown3 = 2,
                              Target = specialTarget,
                              Unknown4 = 0x96,
                              Unknown5 = 4
                          };
            CollectionAssert.AreEqual(
                HexToBytes("754F11150000C3503CAC6F140000000006000004E1000000020000C350782BC5460000009600000004"),
                Serialize(special));

            var containerAdd = new ContainerAddItemMessage
                               {
                                   Identity = actor,
                                   Unknown = 0,
                                   SourceContainer = sourceSlot,
                                   Target = actor,
                                   TargetPlacement = 0x6F
                               };
            CollectionAssert.AreEqual(
                HexToBytes("47537A240000C35000000012000000006B007C00000000C350000000120000006F"),
                Serialize(containerAdd));

            var moveItem = new ClientMoveItemToInventoryMessage
                           {
                               Identity = actor,
                               Unknown = 1,
                               SourceContainer = sourceSlot,
                               TargetPlacement = 0x6F
                           };
            CollectionAssert.AreEqual(
                HexToBytes("5469373F0000C35000000012010000006B007C00000000006F"),
                Serialize(moveItem));
        }

        [TestMethod]
        public void ToClientQuitDespawnAndDropDynelUseDistinctWireShapes()
        {
            CollectionAssert.AreEqual(HexToBytes("36510078"), Serialize(new ToClientQuitMessage()));

            var despawn = new DespawnMessage
                          {
                              Identity = new Identity { Type = IdentityType.Corpse, Instance = 0x00F0F002 },
                              Unknown = 1
                          };
            byte[] capturedDespawn = HexToBytes("365100780000C76A00F0F00201");

            CollectionAssert.AreEqual(capturedDespawn, Serialize(despawn));

            var decodedDespawn = (DespawnMessage)Deserialize<DespawnMessage>(capturedDespawn);
            Assert.AreEqual(IdentityType.Corpse, decodedDespawn.Identity.Type);
            Assert.AreEqual(0x00F0F002, decodedDespawn.Identity.Instance);
            Assert.AreEqual(1, decodedDespawn.Unknown);

            var message = new DropDynelMessage
                          {
                              Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 1234 },
                              Position = new Vector3 { X = 1.0f, Y = 2.0f, Z = 3.0f }
                          };
            byte[] expected = HexToBytes("474836330000C350000004D23F8000004000000040400000");

            CollectionAssert.AreEqual(expected, Serialize(message));

            var decoded = (DropDynelMessage)Deserialize<DropDynelMessage>(expected);
            Assert.AreEqual(IdentityType.CanbeAffected, decoded.Identity.Type);
            Assert.AreEqual(1234, decoded.Identity.Instance);
            Assert.AreEqual(1.0f, decoded.Position.X, 0.0001f);
            Assert.AreEqual(2.0f, decoded.Position.Y, 0.0001f);
            Assert.AreEqual(3.0f, decoded.Position.Z, 0.0001f);
        }

        [TestMethod]
        public void PlayfieldAnarchyFNoGeneratorSizeMatchesLiveShape()
        {
            AssertSerializedSize(
                new PlayfieldAnarchyFMessage
                    {
                        Identity = new Identity { Type = IdentityType.Playfield2, Instance = 0x11E6 },
                        Unknown = 0,
                        Unknown1 = 4,
                        CharacterCoordinates = new Vector3 { X = 940.0f, Y = 20.0f, Z = 732.0f },
                        Unknown2 = 0x61,
                        PlayfieldId1 = new Identity { Type = IdentityType.Playfield1, Instance = 0x11E6 },
                        Unknown3 = 0,
                        Unknown4 = 0,
                        PlayfieldId2 = new Identity { Type = IdentityType.Playfield2, Instance = 0x11E6 },
                        Unknown5 = 0,
                        Unknown6 = 0,
                        PlayfieldX = 100000,
                        PlayfieldZ = 100000
                    },
                70);
        }

        [TestMethod]
        public void OfficialLivePlayfieldAnarchyFGeneratorPayloadRoundTripsOpaque()
        {
            byte[] body = HexToBytes(
                "5F4B1A3900009C5000074B5000000000044561918542510F5B444453BA610000C79E00001999000000010000000000009C5000074B500000C77D000000010000000100000001000000050000C748000000010000000000000001107CD5190000C73D00000001000000010000000156D9B48B0000C75B00000001000000020000000812D1BF190000C73D000000010000000A0000001056D9B48C0000C748000000010000001A00000001107CD51AFFFFFFFFFFFFFFFF");

            PlayfieldAnarchyFMessage decoded = (PlayfieldAnarchyFMessage)Deserialize<PlayfieldAnarchyFMessage>(body);
            Assert.AreEqual((IdentityType)0x0000C79E, decoded.PlayfieldId1.Type);
            Assert.AreEqual(0x1999, decoded.PlayfieldId1.Instance);
            Assert.AreEqual(128, decoded.GeneratorPayload.Length);
            CollectionAssert.AreEqual(body, Serialize(decoded));
        }

        [TestMethod]
        public void CharDCMoveTailFieldsAreFloatAuxValues()
        {
            var message = new CharDCMoveMessage
                          {
                              Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 0x01020304 },
                              Unknown = 1,
                              MoveType = 0x07,
                              Heading =
                                  new Quaternion
                                  {
                                      X = 0.0f,
                                      Y = 0.5f,
                                      Z = 0.0f,
                                      W = 1.0f
                                  },
                              Coordinates = new Vector3 { X = 10.25f, Y = 20.5f, Z = 30.75f },
                              Unknown1 = 12345,
                              AuxA = 1.5f,
                              AuxB = -2.25f
                          };

            byte[] expected = HexToBytes(
                "541111230000C350010203040107000000003F000000000000003F8000004124000041A4000041F60000000030393FC00000C0100000");

            CollectionAssert.AreEqual(expected, Serialize(message));

            var decoded = (CharDCMoveMessage)Deserialize<CharDCMoveMessage>(expected);
            Assert.AreEqual(1.5f, decoded.AuxA, 0.0001f);
            Assert.AreEqual(-2.25f, decoded.AuxB, 0.0001f);
        }

        [TestMethod]
        public void OfficialFollowTargetCoordinateShapeMatchesCapturedChase()
        {
            var message = new FollowTargetMessage
                          {
                              Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 0x788DA3B9 },
                              Unknown = 0,
                              Info =
                                  new FollowCoordinateInfo
                                  {
                                      FollowInfoType = 1,
                                      MoveMode = 25,
                                      Coordinates =
                                          new System.Collections.Generic.List<Vector3>
                                          {
                                              new Vector3 { X = 1.0f, Y = 2.0f, Z = 3.0f },
                                              new Vector3 { X = 4.0f, Y = 5.0f, Z = 6.0f }
                                          }
                                  }
                          };

            byte[] expected = HexToBytes(
                "260F36710000C350788DA3B9000119023F80000040000000404000004080000040A0000040C00000");

            CollectionAssert.AreEqual(expected, Serialize(message));

            var decoded = (FollowTargetMessage)Deserialize<FollowTargetMessage>(expected);
            var info = (FollowCoordinateInfo)decoded.Info;
            Assert.AreEqual(0, decoded.Unknown);
            Assert.AreEqual(1, info.FollowInfoType);
            Assert.AreEqual(25, info.MoveMode);
            Assert.AreEqual(2, info.CoordinateCount);
            Assert.AreEqual(2, info.Coordinates.Count);
        }

        [TestMethod]
        public void PrivateReferenceFollowTargetCoordinateShapeSupportsMultiPointPaths()
        {
            var message = new FollowTargetMessage
                          {
                              Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 0x000F71FF },
                              Unknown = 1,
                              Info =
                                  new FollowCoordinateInfo
                                  {
                                      FollowInfoType = 1,
                                      MoveMode = 24,
                                      Coordinates =
                                          new System.Collections.Generic.List<Vector3>
                                          {
                                              new Vector3 { X = 1.0f, Y = 2.0f, Z = 3.0f },
                                              new Vector3 { X = 4.0f, Y = 5.0f, Z = 6.0f },
                                              new Vector3 { X = 7.0f, Y = 8.0f, Z = 9.0f }
                                          }
                                  }
                          };

            byte[] bytes = Serialize(message);
            Assert.AreEqual(52, bytes.Length);
            CollectionAssert.AreEqual(
                HexToBytes("260F36710000C350000F71FF01011803"),
                SubArray(bytes, 0, 16));

            var decoded = (FollowTargetMessage)Deserialize<FollowTargetMessage>(bytes);
            var info = (FollowCoordinateInfo)decoded.Info;
            Assert.AreEqual(1, decoded.Unknown);
            Assert.AreEqual(24, info.MoveMode);
            Assert.AreEqual(3, info.CoordinateCount);
            Assert.AreEqual(3, info.Coordinates.Count);
        }

        [TestMethod]
        public void FollowTargetType2StopSettleShapeMatchesCapturedEvidence()
        {
            var message = new FollowTargetMessage
                          {
                              Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 0x000F66D9 },
                              Unknown = 0,
                              Info =
                                  new FollowStopInfo
                                  {
                                      MoveType = 21,
                                      Unknown1 = 0,
                                      Unknown2 = 0,
                                      Unknown3 = 0,
                                      Coordinates = new Vector3 { X = 1.0f, Y = 2.0f, Z = 3.0f },
                                      Flag = 1,
                                      ConfirmCoordinates = new Vector3 { X = 1.0f, Y = 2.0f, Z = 3.0f }
                                  }
                          };

            byte[] expected = HexToBytes(
                "260F36710000C350000F66D90002150000000000000000000000003F8000004000000040400000013F8000004000000040400000");

            CollectionAssert.AreEqual(expected, Serialize(message));

            var decoded = (FollowTargetMessage)Deserialize<FollowTargetMessage>(expected);
            var info = (FollowStopInfo)decoded.Info;
            Assert.AreEqual(0, decoded.Unknown);
            Assert.AreEqual(2, info.FollowInfoType);
            Assert.AreEqual(21, info.MoveType);
            Assert.AreEqual(1, info.Flag);
        }

        private static void AssertSerializedSize(MessageBody body, int expectedSize)
        {
            byte[] bytes = Serialize(body);
            Assert.AreEqual(expectedSize, bytes.Length, body.GetType().Name);
        }

        private static byte[] Serialize(MessageBody body)
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(body.GetType());

            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream))
                {
                    serializer.Serialize(writer, new SerializationContext(resolver), body);
                    return memoryStream.ToArray();
                }
            }
        }

        private static MessageBody Deserialize<T>(byte[] bytes)
            where T : MessageBody
        {
            var resolver = new SerializerResolverBuilder<MessageBody>().Build();
            var serializer = resolver.GetSerializer(typeof(T));

            using (var memoryStream = new MemoryStream(bytes))
            {
                using (var reader = new StreamReader(memoryStream))
                {
                    return (MessageBody)serializer.Deserialize(reader, new SerializationContext(resolver));
                }
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            }

            return bytes;
        }

        private static byte[] SubArray(byte[] source, int offset, int count)
        {
            byte[] result = new byte[count];
            System.Array.Copy(source, offset, result, 0, count);
            return result;
        }
    }
}
