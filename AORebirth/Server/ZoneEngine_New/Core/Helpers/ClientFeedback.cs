namespace ZoneEngine_New.Core.Helpers
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Unformatted client feedback. The client looks the line up with
    /// GetText(category 110, ELF hash of the Feedback_* key) and shows it on channel 0x42000018.
    /// </summary>
    internal static class ClientFeedback
    {
        /// <summary>System-feedback window. The client passes this to its own feedback helper.</summary>
        public const int Channel = 0x42000018;

        public const int CategoryId = 110;

        public static void Send(Character character, string key)
            => Send(character, unchecked((int)ElfHash(key)));

        static void Send(Character character, int messageId)
        {
            if (character is not Player player || player.Session == null)
                return;

            player.Session.Send(Create(player.Identity, messageId));
        }

        public static FeedbackMessage Create(Identity identity, string key)
            => Create(identity, unchecked((int)ElfHash(key)));

        static FeedbackMessage Create(Identity identity, int messageId)
            => new()
            {
                Identity = identity,
                Unknown = 1,
                Unknown1 = Channel,
                CategoryId = CategoryId,
                MessageId = messageId
            };

        public static uint ElfHash(string text)
        {
            uint hash = 0;
            foreach (char c in text)
            {
                hash = (hash << 4) + c;
                uint high = hash & 0xF0000000;
                if (high != 0)
                    hash ^= high >> 24;
                hash &= ~high;
            }

            return hash;
        }
    }
}
