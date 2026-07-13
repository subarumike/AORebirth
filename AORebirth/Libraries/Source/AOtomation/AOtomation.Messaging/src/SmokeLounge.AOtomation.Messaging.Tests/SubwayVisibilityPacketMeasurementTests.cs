namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using ZoneEngine.Core.Playfields;

    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    [TestClass]
    public class SubwayVisibilityPacketMeasurementTests
    {
        [TestMethod]
        public void SerializedSizeMeasurementDoesNotAlterPacketBytes()
        {
            byte[] payload =
                {
                    0xDF, 0xDF, 0x00, 0x01, 0x12, 0x34, 0x56, 0x78,
                    0x3B, 0x1D, 0x22, 0x68, 0x00, 0x00, 0x00, 0x00
                };
            byte[] before = (byte[])payload.Clone();

            int measured = SubwayVisibilityPacketMeasurement.MeasureSerializedBytes(payload);

            Assert.AreEqual(payload.Length, measured);
            CollectionAssert.AreEqual(before, payload);
            Assert.AreEqual(0, SubwayVisibilityPacketMeasurement.MeasureSerializedBytes(null));
        }

        [TestMethod]
        public void SimpleCharFullUpdateFixtureRetainsExactBytesAcrossVisibilityMeasurement()
        {
            AssertExactFixture(
                "271B3A6B0000C3507953A9B6003A0000000043A8E60642CCD53243757B85022B4ACB"
                + "3F172E65000000003F4E978A0000062B1241726368697465637420537472696B657200"
                + "100812010000000095000000000D01470000031BDF0060001F000000001C0000000000"
                + "000000000000000301000100010001000100000002000000009EFA2D000003F10000C350"
                + "7953A9B60000000143A8E60642CCD53243757B85000017A6000000000000A0100000000"
                + "0000000010000582200000000000000020000A008000000000000000300005809000000"
                + "00000000040000585A00000000000007E20000009EFA00000000040000000000",
                CreateSimpleCharFullUpdateFixture());
        }

        [TestMethod]
        public void WeaponDefinitionFixtureRetainsExactBytesAcrossVisibilityMeasurement()
        {
            AssertExactFixture(
                "3B1D22680000C3507953A9B600000000000000C3507953A9B60000007F0000C350"
                + "000000010000000003F100000000",
                CreateWeaponDefinitionFixture());
        }

        [TestMethod]
        public void CharInPlayFixtureRetainsExactBytesAcrossVisibilityMeasurement()
        {
            AssertExactFixture("570C20390000C3507953A9B600", CreateCharInPlayFixture());
        }

        private static SimpleCharFullUpdateMessage CreateSimpleCharFullUpdateFixture()
        {
            var capturedFlags = (SimpleCharFullUpdateFlags)0x022B4ACB;
            return new SimpleCharFullUpdateMessage
                   {
                       Identity = new Identity
                                  {
                                      Type = IdentityType.CanbeAffected,
                                      Instance = unchecked((int)0x7953A9B6)
                                  },
                       Version = 58,
                       Coordinates = new Vector3
                                     {
                                         X = 337.797058f,
                                         Y = 102.4164f,
                                         Z = 245.4825f
                                     },
                       Heading = new Quaternion
                                 {
                                     X = 0.0f,
                                     Y = 0.5905517f,
                                     Z = 0.0f,
                                     W = 0.8069998f
                                 },
                       Appearance = new Appearance { Value = 1579 },
                       Name = "Architect Striker",
                       CharacterFlags = (CharacterFlags)268964353,
                       CharacterInfo = new SimpleNpcInfo { Family = 149, LosHeight = 0 },
                       Level = 13,
                       Health = 327,
                       HealthDamage = 0,
                       MonsterData = 203743,
                       MonsterScale = 96,
                       VisualFlags = 31,
                       Unknown1 = new byte[]
                                  {
                                      0, 0, 0, 0, 0, 0, 0, 0,
                                      0, 0, 0, 0, 3, 1, 0, 1,
                                      0, 1, 0, 1, 0, 1, 0, 0,
                                      0, 2, 0, 0
                                  },
                       HeadMesh = 40698,
                       RunSpeedBase = 45,
                       ActiveNanos = new ActiveNano[0],
                       Waypoints = new[]
                                   {
                                       new Vector3
                                       {
                                           X = 337.797058f,
                                           Y = 102.4164f,
                                           Z = 245.4825f
                                       }
                                   },
                       Textures = new[]
                                  {
                                      new Texture { Place = 0, Id = 40976, Unknown = 0 },
                                      new Texture { Place = 1, Id = 22562, Unknown = 0 },
                                      new Texture { Place = 2, Id = 40968, Unknown = 0 },
                                      new Texture { Place = 3, Id = 22537, Unknown = 0 },
                                      new Texture { Place = 4, Id = 22618, Unknown = 0 }
                                  },
                       Meshes = new[]
                                {
                                    new Mesh
                                    {
                                        Position = 0,
                                        Id = 40698,
                                        OverrideTextureId = 0,
                                        Layer = 4
                                    }
                                },
                       AdditionalFlags = capturedFlags,
                       SuppressedFlags = ~capturedFlags,
                       Flags2 = 0,
                       Unknown2 = 0
                   };
        }

        private static WeaponItemFullUpdateMessage CreateWeaponDefinitionFixture()
        {
            return new WeaponItemFullUpdateMessage
                   {
                       Identity = new Identity
                                  {
                                      Type = IdentityType.CanbeAffected,
                                      Instance = unchecked((int)0x7953A9B6)
                                  },
                       Unknown = 0,
                       Unknown1 = 0,
                       Owner = new Identity
                               {
                                   Type = IdentityType.CanbeAffected,
                                   Instance = unchecked((int)0x7953A9B6)
                               },
                       PlayfieldId = 127,
                       StateMachine = new Identity
                                      {
                                          Type = IdentityType.CanbeAffected,
                                          Instance = 1
                                      },
                       Unknown2 = 0,
                       Stats = new GameTuple<CharacterStat, uint>[0],
                       Unknown3 = 0
                   };
        }

        private static CharInPlayMessage CreateCharInPlayFixture()
        {
            return new CharInPlayMessage
                   {
                       Identity = new Identity
                                  {
                                      Type = IdentityType.CanbeAffected,
                                      Instance = unchecked((int)0x7953A9B6)
                                  },
                       Unknown = 0
                   };
        }

        private static void AssertExactFixture(string expectedHex, MessageBody fixture)
        {
            byte[] before = Serialize(fixture);
            Assert.AreEqual(expectedHex, ToHex(before));
            byte[] measuredBuffer = (byte[])before.Clone();

            Assert.AreEqual(
                measuredBuffer.Length,
                SubwayVisibilityPacketMeasurement.MeasureSerializedBytes(measuredBuffer));
            CollectionAssert.AreEqual(before, measuredBuffer);

            byte[] after = Serialize(fixture);
            CollectionAssert.AreEqual(before, after);
            Assert.AreEqual(expectedHex, ToHex(after));
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

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }
    }
}
