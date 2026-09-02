namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Text;

    internal static class TilemapPayloadLocator
    {
        private static readonly byte[] GndaMagic = Encoding.ASCII.GetBytes("GNDA");
        private static readonly byte[] ChgaMagic = Encoding.ASCII.GetBytes("CHGA");

        internal static bool TryGetGndaPayload(byte[] rawRecord, out byte[] payload)
        {
            return TryGetPayload(rawRecord, GndaMagic, out payload);
        }

        internal static bool TryGetChgaPayload(byte[] rawRecord, out byte[] payload)
        {
            return TryGetPayload(rawRecord, ChgaMagic, out payload);
        }

        private static bool TryGetPayload(byte[] rawRecord, byte[] magic, out byte[] payload)
        {
            payload = null;
            if (rawRecord == null || rawRecord.Length < magic.Length)
            {
                return false;
            }

            int offset = IndexOf(rawRecord, magic);
            if (offset < 0)
            {
                return false;
            }

            payload = new byte[rawRecord.Length - offset];
            Buffer.BlockCopy(rawRecord, offset, payload, 0, payload.Length);
            return true;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int index = 0; index <= haystack.Length - needle.Length; index++)
            {
                bool matched = true;
                for (int needleIndex = 0; needleIndex < needle.Length; needleIndex++)
                {
                    if (haystack[index + needleIndex] != needle[needleIndex])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
