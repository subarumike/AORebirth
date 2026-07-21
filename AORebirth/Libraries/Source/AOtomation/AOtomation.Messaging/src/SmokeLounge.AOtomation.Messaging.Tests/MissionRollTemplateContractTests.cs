namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

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
    }
}
