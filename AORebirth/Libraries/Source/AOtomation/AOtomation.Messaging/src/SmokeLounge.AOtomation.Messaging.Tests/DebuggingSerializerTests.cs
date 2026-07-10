// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DebuggingSerializerTests.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the DebuggingSerializerTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom;

    using StreamReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    [TestClass]
    public class DebuggingSerializerTests
    {
        #region Public Methods and Operators

        [TestMethod]
        public void CharacterListMessageTest()
        {
            var expected = new CharacterListMessage
                               {
                                   Characters =
                                       new[]
                                           {
                                               new LoginCharacterInfo
                                                   {
                                                       Name = "Trolololo", 
                                                       AreaName = "ICC", 
                                                       PlayfieldId = Identity.None, 
                                                       ExitDoorId = Identity.None
                                                   }, 
                                               new LoginCharacterInfo
                                                   {
                                                       Name = "Haiguise", 
                                                       AreaName = "Bore", 
                                                       PlayfieldId = Identity.None, 
                                                       ExitDoorId = Identity.None
                                                   }
                                           }
                               };

            var actual = (CharacterListMessage)this.SerializeDeserialize(expected);

            Assert.AreEqual(expected.AllowedCharacters, actual.AllowedCharacters);
            Assert.AreEqual(expected.Characters.Length, actual.Characters.Length);

            var expectedChars = expected.Characters.GetEnumerator();
            var actualChars = actual.Characters.GetEnumerator();

            while (expectedChars.MoveNext())
            {
                actualChars.MoveNext();
                var expectedChar = (LoginCharacterInfo)expectedChars.Current;
                var actualChar = (LoginCharacterInfo)actualChars.Current;

                Assert.AreEqual(expectedChar.AreaName, actualChar.AreaName);
                Assert.AreEqual(expectedChar.Name, actualChar.Name);
            }

            Assert.AreEqual(expected.Expansions, actual.Expansions);
        }

        [TestMethod]
        public void SimpleCharFullUpdateSerializerHonorsCapturedOrdinaryFlagAndVisualShape()
        {
            var capturedFlags = (SimpleCharFullUpdateFlags)0x022B4ACB;
            var expectedUnknown1 =
                new byte[]
                    {
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x03, 0x01, 0x00, 0x01,
                        0x00, 0x01, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                        0x00, 0x02, 0x00, 0x00
                    };
            var message =
                new SimpleCharFullUpdateMessage
                    {
                        Identity = new Identity { Type = IdentityType.CanbeAffected, Instance = 0x7953A9B6 },
                        Version = 58,
                        Coordinates = new Vector3 { X = 337.797058f, Y = 102.4164f, Z = 245.4825f },
                        Heading = new Quaternion { X = 0, Y = 0.5905517f, Z = 0, W = 0.8069998f },
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
                        Unknown1 = expectedUnknown1,
                        HeadMesh = 40698,
                        RunSpeedBase = 45,
                        ActiveNanos = new ActiveNano[0],
                        Waypoints =
                            new[]
                                {
                                    new Vector3 { X = 337.797058f, Y = 102.4164f, Z = 245.4825f }
                                },
                        Textures =
                            new[]
                                {
                                    new Texture { Place = 0, Id = 40976, Unknown = 0 },
                                    new Texture { Place = 1, Id = 22562, Unknown = 0 },
                                    new Texture { Place = 2, Id = 40968, Unknown = 0 },
                                    new Texture { Place = 3, Id = 22537, Unknown = 0 },
                                    new Texture { Place = 4, Id = 22618, Unknown = 0 }
                                },
                        Meshes =
                            new[]
                                {
                                    new Mesh { Position = 0, Id = 40698, OverrideTextureId = 0, Layer = 4 }
                                },
                        AdditionalFlags = capturedFlags,
                        SuppressedFlags = ~capturedFlags,
                        Flags2 = 0,
                        Unknown2 = 0
                    };

            var serializerResolver = new DebuggingSerializerResolverBuilder<MessageBody>().Build();
            var serializationContext = new SerializationContext(serializerResolver);
            using (var memoryStream = new MemoryStream())
            using (var streamWriter = new StreamWriter(memoryStream))
            using (var streamReader = new StreamReader(memoryStream))
            {
                new SimpleCharFullUpdateSerializer().Serialize(
                    streamWriter,
                    serializationContext,
                    message);
                memoryStream.Position = 30;
                Assert.AreEqual((int)capturedFlags, streamReader.ReadInt32());
            }
        }

        #endregion

        #region Methods

        private object SerializeDeserialize(object obj)
        {
            MemoryStream memoryStream = null;

            var serializerResolver = new DebuggingSerializerResolverBuilder<MessageBody>().Build();
            var serializer = serializerResolver.GetSerializer(obj.GetType());

            try
            {
                memoryStream = new MemoryStream();
                using (var streamWriter = new StreamWriter(memoryStream))
                using (var streamReader = new StreamReader(memoryStream))
                {
                    var serializationContext = new SerializationContext(serializerResolver);
                    serializer.Serialize(streamWriter, serializationContext, obj);
                    var arr = memoryStream.ToArray();
                    Console.WriteLine(BitConverter.ToString(arr));

                    memoryStream.Position = 0;
                    var deserializationContext = new SerializationContext(serializerResolver);
                    var result = serializer.Deserialize(streamReader, deserializationContext);
                    memoryStream = null;
                    return result;
                }
            }
            finally
            {
                if (memoryStream != null)
                {
                    memoryStream.Dispose();
                }
            }
        }

        #endregion
    }
}
