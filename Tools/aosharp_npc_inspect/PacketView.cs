namespace NpcInspectProbe
{
    // Shallow framing only; deliberately does not interpret equipment payloads.
    public static class PacketView
    {
        public const uint InspectKey = 0x5A585F65;
        public const uint CharacterActionKey = 0x5E477770;
        public const uint FeedbackKey = 0x50544D19;

        public static string Describe(byte[] data)
        {
            if (data == null || data.Length < 16) return "header=SHORT";
            int family = (data[2] << 8) | data[3];
            string result = "family=" + family + " headerLength=" + HeaderLength(data)
                + " sender=" + U32(data, 8).ToString("X8") + " receiver=" + U32(data, 12).ToString("X8");
            if (family == 10 && data.Length >= 29)
                result += " key=" + U32(data, 16).ToString("X8") + " inheritedIdentity="
                    + U32(data, 20) + ":" + U32(data, 24).ToString("X8") + " passOn=" + data[28];
            return result; // Describes bytes, not a claim of valid framing.
        }

        public static bool TryFeedback(byte[] data, out uint unknown, out uint category, out uint message)
        {
            unknown = category = message = 0;
            if (!IsN3(data, FeedbackKey, 41)) return false;
            unknown = U32(data, 29);
            category = U32(data, 33);
            message = U32(data, 37);
            return true;
        }

        public static bool IsInspectRejection(uint category, uint message)
        {
            // Local text.mdb lookup, not an inferred NPC-specific reason.
            return category == 110 && message == 0x030C85E4;
        }

        public static uint U32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        public static bool IsN3(byte[] data, uint key, int minimumLength)
        {
            return HasN3Layout(data, key, minimumLength)
                && HeaderLength(data) == data.Length;
        }

        public static int HeaderLength(byte[] data)
        {
            return (data[6] << 8) | data[7];
        }

        private static bool HasN3Layout(byte[] data, uint key, int minimumLength)
        {
            // Both directions have key at 16, identity at 20, pass-on at 28.
            return data != null && data.Length >= System.Math.Max(29, minimumLength)
                && data.Length <= ushort.MaxValue
                && data[2] == 0 && data[3] == 10
                && U32(data, 16) == key;
        }

        public static bool IsOutgoingN3(byte[] data, uint key, int minimumLength)
        {
            // Zam Bootstrap.Send_Hook copies the bytes BEFORE Connection.Send
            // fills the transport header. MessageProtocol leaves bytes 4..7
            // zero. Also accept already-framed managed sends, never arbitrary
            // nonzero size mismatches. Incoming validation remains strict.
            return HasN3Layout(data, key, minimumLength)
                && (HeaderLength(data) == data.Length
                    || (data[4] == 0 && data[5] == 0 && HeaderLength(data) == 0));
        }

        public static bool IsInspectReply(byte[] data, int type, int instance)
        {
            return IsN3(data, InspectKey, 37)
                && U32(data, 29) == unchecked((uint)type)
                && U32(data, 33) == unchecked((uint)instance);
        }

        public static bool IsInspectRequest(byte[] data, int actorType, int actorInstance,
            int targetType, int targetInstance)
        {
            // Native CharacterAction serializer: action, value, target,
            // extra identity, uint16 string size, string.
            return IsOutgoingN3(data, CharacterActionKey, 55)
                && U32(data, 20) == unchecked((uint)actorType)
                && U32(data, 24) == unchecked((uint)actorInstance)
                && U32(data, 29) == 0x105
                && U32(data, 37) == unchecked((uint)targetType)
                && U32(data, 41) == unchecked((uint)targetInstance)
                && 55 + ((data[53] << 8) | data[54]) == data.Length;
        }
    }
}
