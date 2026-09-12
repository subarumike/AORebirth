namespace ZoneEngine.Core
{
    using System;

    internal static partial class FlintKneecappingTipWire
    {
        private const int CapturedCharacterInstance = 1985636618;

        private const int CapturedDeleteMissionInstance = 1431734586;

        private const string Action59DeleteHex =
            "0135000A0001003700000DB6765A690A5E4777700000C350765A690A000000003B000000000000DAC35556893A0000DAC35556893A0000";

        private const string QuestDeleteHex =
            "0136000A0001003500000DB6765A690A212C487A0000C350765A690A0000000001000000000000DAC35556893A0000000000000000";

        private static void ReplaceInstance(byte[] packet, int from, int to)
        {
            byte b0 = (byte)(from >> 24);
            byte b1 = (byte)(from >> 16);
            byte b2 = (byte)(from >> 8);
            byte b3 = (byte)from;
            for (int i = 0; i + 4 <= packet.Length; i++)
            {
                if (packet[i] == b0 && packet[i + 1] == b1 && packet[i + 2] == b2 && packet[i + 3] == b3)
                {
                    packet[i] = (byte)(to >> 24);
                    packet[i + 1] = (byte)(to >> 16);
                    packet[i + 2] = (byte)(to >> 8);
                    packet[i + 3] = (byte)to;
                    i += 3;
                }
            }
        }

        private static byte[] Hex(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        internal static byte[][] CreateDeletePackets(int recipientInstance, int missionInstance)
        {
            byte[] action59 = Hex(Action59DeleteHex);
            ReplaceInstance(action59, CapturedCharacterInstance, recipientInstance);
            ReplaceInstance(action59, CapturedDeleteMissionInstance, missionInstance);
            byte[] questDelete = Hex(QuestDeleteHex);
            ReplaceInstance(questDelete, CapturedCharacterInstance, recipientInstance);
            ReplaceInstance(questDelete, CapturedDeleteMissionInstance, missionInstance);
            return new[] { action59, questDelete };
        }
    }
}
