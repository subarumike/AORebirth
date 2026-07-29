namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Guards the mission-roll template used by <see cref="MissionRollService"/>. The service decodes a
    /// captured server->client QuestAlternative response into live objects and re-serializes it back to the
    /// client. If our QuestAlternative/QuestInfo serializer does not reproduce the captured bytes exactly,
    /// the client silently rejects the reply and the mission terminal shows an empty list. This test proves
    /// the decode/encode round-trip is byte-for-byte faithful.
    /// </summary>
    [TestClass]
    public class MissionRollTemplateContractTests
    {
        private static readonly string[] CapturedRollHashes =
        {
            "152ceabb1536715df69883c5d5088eacb213ecf61fb4f9c2d2f550e9c9cb4e06",
            "3ddb22e89fb0f9e2176556be6764f8fd02150e77ec51baf69e5bdc52ab234462",
            "4f622acee4b21c66d4439ff583405023d1eca248b4464620bdf605fdd6fa3d51",
            "71eb2541a23457abb1e73a2d9de8a1472d3697f53f6874b5452ab69874536a00",
            "55efb5d88472cddaa788cf2892c96b30b40f1764fa2c2164bc3da1635bf504fd",
            "967ad2f51ea32de6304529dc2dcb5b6d50d2d19f7d7a2d620e02bc96f92960ba",
            "a94bad91faa77e8d7915ccbe8f065593d7e3fb3602011b1b652c759c24c00129",
            "5dbaa64c92660b085263f74942c8272ae828ff055ea46b1207f6c70c27ddf8c5",
            "5445d5cdbfe9471952c03d3a23de1382ecba4c885ccf6c277850bc1dd1e56032",
            "08650d6dad4458ca02b6a255ad2ddc530fd05fb0d200a85de31352e2842ca9d5",
            "786d807fafa7cd42a9d21400c029bec5d9f65922532b88ea878453a7615577fd",
            "33173d02d87ba1e973b7b8bd3489c510f5983468ac7aaf3cb8a5a995f8a4956a",
            "c2847ffbb9d04264e8771e5565fba303577e187ace693266051874281661536f"
        };

        [TestMethod]
        public void CapturedQuestAlternativeTemplateRoundTripsByteForByte()
        {
            byte[] body = MissionRollService.TemplateBody;

            QuestAlternativeMessage decoded = MissionRollService.DecodeTemplate();

            Assert.IsNotNull(decoded.QuestInfos, "QuestInfos should not be null after decode.");
            Assert.AreEqual(5, decoded.QuestInfos.Length, "Captured template should decode to 5 offers.");

            byte[] reserialized = MissionRollService.SerializeBody(decoded);

            int firstDiff = FirstDifference(body, reserialized);
            Assert.AreEqual(-1, firstDiff, DescribeDifference(body, reserialized, firstDiff));
        }

        [TestMethod]
        public void EveryCapturedRollBodyRoundTripsAndMatchesItsGoldenHash()
        {
            Assert.AreEqual(CapturedRollHashes.Length, MissionRollService.CapturedRollCount);
            using (SHA256 sha256 = SHA256.Create())
            {
                for (int i = 0; i < MissionRollService.CapturedRollCount; i++)
                {
                    byte[] body = MissionRollService.CapturedRollBody(i);
                    QuestAlternativeMessage decoded = MissionRollService.DecodeCapturedRoll(i);

                    Assert.IsNotNull(decoded.QuestInfos, "roll " + i + " offers");
                    Assert.AreEqual(5, decoded.QuestInfos.Length, "roll " + i + " offer count");
                    Assert.AreEqual(
                        CapturedRollHashes[i],
                        Hex(sha256.ComputeHash(body)),
                        "roll " + i + " fixture hash");

                    byte[] reserialized = MissionRollService.SerializeBody(decoded);
                    int firstDiff = FirstDifference(body, reserialized);
                    Assert.AreEqual(
                        -1,
                        firstDiff,
                        "roll " + i + ": " + DescribeDifference(body, reserialized, firstDiff));
                }
            }
        }

        [TestMethod]
        public void CapturedFixtureAccessorsReturnDefensiveCopies()
        {
            byte[] template = MissionRollService.TemplateBody;
            byte originalTemplateByte = template[0];
            template[0] ^= 0xff;
            Assert.AreEqual(originalTemplateByte, MissionRollService.TemplateBody[0]);

            byte[] roll = MissionRollService.CapturedRollBody(0);
            byte originalRollByte = roll[0];
            roll[0] ^= 0xff;
            Assert.AreEqual(originalRollByte, MissionRollService.CapturedRollBody(0)[0]);

            string[] hexBodies = MissionRollCaptureLibrary.CapturedRollBodiesHex;
            string originalHex = hexBodies[0];
            hexBodies[0] = string.Empty;
            Assert.AreEqual(originalHex, MissionRollCaptureLibrary.CapturedRollBodiesHex[0]);
        }

        [TestMethod]
        public void GeneratedRollPreservesOneCompleteCapturedServerResponseEnvelope()
        {
            const int capturedResponseIndex = 7;
            QuestAlternativeMessage captured =
                MissionRollService.DecodeCapturedRoll(capturedResponseIndex);
            var request = new QuestAlternativeMessage
                          {
                              Identity = new Identity
                                         {
                                             Type = (IdentityType)50000,
                                             Instance = 22
                                         },
                              MissionTerminalIdentity =
                                  new Identity
                                  {
                                      Type = (IdentityType)56001,
                                      Instance = unchecked((int)0xC000028F)
                                  },
                              VersionId = 4,
                              LevelSlider = 1,
                              GoodBadSlider = 0,
                              OrderChaosSlider = 0,
                              OpenHiddenSlider = 0,
                              PhysicalMysticalSlider = 0,
                              HeadOnStealthSlider = 0,
                              MoneyExperienceSlider = 0,
                              Unknown4 = 0,
                              Unknown5 = 1,
                              QuestInfos = new QuestInfo[0]
                          };

            QuestAlternativeMessage generated =
                MissionRollService.BuildRollResponseDeterministic(
                    request,
                    request.Identity,
                    4,
                    100,
                    0f,
                    0f,
                    MissionLocationSide.Omni,
                    12345,
                    capturedResponseIndex,
                    unchecked((int)0x55690000),
                    1201445827);

            Assert.AreEqual(captured.VersionId, generated.VersionId);
            Assert.AreEqual(captured.LevelSlider, generated.LevelSlider);
            Assert.AreEqual(captured.GoodBadSlider, generated.GoodBadSlider);
            Assert.AreEqual(captured.OrderChaosSlider, generated.OrderChaosSlider);
            Assert.AreEqual(captured.OpenHiddenSlider, generated.OpenHiddenSlider);
            Assert.AreEqual(captured.PhysicalMysticalSlider, generated.PhysicalMysticalSlider);
            Assert.AreEqual(captured.HeadOnStealthSlider, generated.HeadOnStealthSlider);
            Assert.AreEqual(captured.MoneyExperienceSlider, generated.MoneyExperienceSlider);
            Assert.AreEqual(captured.Unknown4, generated.Unknown4);
            Assert.AreEqual(captured.Unknown5, generated.Unknown5);
            Assert.AreNotEqual(
                request.GoodBadSlider,
                generated.GoodBadSlider,
                "Server response envelope must not echo request-only slider bytes.");
            Assert.AreEqual(request.Identity, generated.Identity);
            Assert.AreEqual(
                request.MissionTerminalIdentity,
                generated.MissionTerminalIdentity);
            Assert.AreEqual(5, generated.QuestInfos.Length);
        }

        [TestMethod]
        public void GeneratedRollPreservesClientTitleWidthFreshExpiryAndRoundTrips()
        {
            var request = new QuestAlternativeMessage
                          {
                              Identity = new Identity
                                         {
                                             Type = (IdentityType)50000,
                                             Instance = 22
                                         },
                              MissionTerminalIdentity =
                                  new Identity
                                  {
                                      Type = (IdentityType)56001,
                                      Instance = unchecked((int)0xC000028F)
                                  },
                              VersionId = 4,
                              LevelSlider = 1,
                              GoodBadSlider = 0,
                              OrderChaosSlider = 0,
                              OpenHiddenSlider = 0,
                              PhysicalMysticalSlider = 0,
                              HeadOnStealthSlider = 0,
                              MoneyExperienceSlider = 0,
                              Unknown4 = 0,
                              Unknown5 = 1,
                              QuestInfos = new QuestInfo[0]
                          };

            QuestAlternativeMessage generated =
                MissionRollService.BuildRollResponseDeterministic(
                    request,
                    request.Identity,
                    4,
                    100,
                    0f,
                    0f,
                    MissionLocationSide.Omni,
                    12345,
                    7,
                    unchecked((int)0x55690000),
                    1201445827);
            foreach (QuestInfo offer in generated.QuestInfos)
            {
                Assert.AreEqual(
                    31,
                    Encoding.ASCII.GetByteCount(offer.ShortInfo),
                    "Captured QuestAlternative titles occupy 31 bytes before the terminator.");
                Assert.AreEqual(
                    -1,
                    offer.ShortInfo.IndexOf('\0'),
                    "A zero-padded short title misaligns the live client reader.");
                Assert.AreEqual(
                    0x51534F52,
                    offer.UnknownHash,
                    "The fixed top-level QSOR tag must remain captured evidence.");
                Assert.AreEqual(
                    1201618627,
                    offer.QuestActions[0].UnknownHash15,
                    "Every offer must expire exactly 48 client-clock hours after this roll.");
            }

            byte[] body = MissionRollService.SerializeBody(generated);

            var builder = new SerializerResolverBuilder<MessageBody>();
            SerializerResolver resolver = builder.Build();
            ISerializer serializer = resolver.GetSerializer(typeof(QuestAlternativeMessage));
            using (var memoryStream = new MemoryStream(body))
            using (var reader =
                new SmokeLounge.AOtomation.Messaging.Serialization.StreamReader(memoryStream))
            {
                QuestAlternativeMessage decoded;
                try
                {
                    decoded = (QuestAlternativeMessage)serializer.Deserialize(
                        reader,
                        new SerializationContext(resolver));
                }
                catch (Exception exception)
                {
                    Assert.Fail(
                        "Generated roll failed to decode at byte "
                        + reader.Position
                        + "/"
                        + reader.Length
                        + ": "
                        + exception);
                    return;
                }

                Assert.AreEqual(body.Length, reader.Position);
                Assert.AreEqual(5, decoded.QuestInfos.Length);
            }
        }

        [TestMethod]
        public void MissionRollDeadlineUsesThePrivateServerGameTimeAnchor()
        {
            var synced = new DateTime(2026, 7, 29, 4, 33, 16, DateTimeKind.Utc);

            Assert.AreEqual(
                1201445832,
                MissionRollService.ResolveClientClockNowSeconds(
                    synced,
                    synced.AddSeconds(5)));
            Assert.AreEqual(
                1201445827,
                MissionRollService.ResolveClientClockNowSeconds(
                    synced,
                    synced.AddSeconds(-5)));
            Assert.AreEqual(
                1201618632,
                MissionRollService.ResolveClientExpirySeconds(
                    synced,
                    synced.AddSeconds(5),
                    synced.AddSeconds(172805)));
            Assert.AreEqual(
                0,
                MissionRollService.ResolveClientExpirySeconds(
                    synced,
                    synced.AddSeconds(5),
                    synced.AddSeconds(4)));
        }

        [TestMethod]
        public void AcceptedMissionExpirySerializesAsFourLosslessWireBytes()
        {
            string expiry =
                MissionRollService.IntToFixedBinaryString(0x479F3EC3);
            MethodInfo fixedStringBytes =
                typeof(QuestFullUpdateMessageSerializer).GetMethod(
                    "FixedStringBytes",
                    BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(fixedStringBytes);

            CollectionAssert.AreEqual(
                new byte[] { 0x47, 0x9F, 0x3E, 0xC3 },
                (byte[])fixedStringBytes.Invoke(null, new object[] { expiry, 4 }));
        }

        private static int FirstDifference(byte[] expected, byte[] actual)
        {
            int min = Math.Min(expected.Length, actual.Length);
            for (int i = 0; i < min; i++)
            {
                if (expected[i] != actual[i])
                {
                    return i;
                }
            }

            return expected.Length == actual.Length ? -1 : min;
        }

        private static string DescribeDifference(byte[] expected, byte[] actual, int offset)
        {
            if (offset < 0)
            {
                return "Byte streams are identical.";
            }

            var sb = new StringBuilder();
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "Length expected={0} actual={1}; first diff at offset {2}. ",
                expected.Length,
                actual.Length,
                offset);
            sb.Append("expected[").Append(Window(expected, offset)).Append("] ");
            sb.Append("actual[").Append(Window(actual, offset)).Append(']');
            return sb.ToString();
        }

        private static string Window(byte[] data, int offset)
        {
            int start = Math.Max(0, offset - 6);
            int end = Math.Min(data.Length, offset + 24);
            var sb = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                if (i > start)
                {
                    sb.Append(' ');
                }

                if (i == offset)
                {
                    sb.Append('>');
                }

                sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        private static string Hex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
