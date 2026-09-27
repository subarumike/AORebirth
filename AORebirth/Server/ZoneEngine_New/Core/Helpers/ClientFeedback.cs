namespace ZoneEngine_New.Core.Helpers
{
    using System.Text;

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

        /// <summary>"You're unable to perform this action; please check the requirements of the item." Key name unknown.</summary>
        public const int CheckItemRequirements = 141178878;

        /// <summary>"Target resisted." (category 110).</summary>
        public const int TargetResisted = 205237300;

        /// <summary>"A too high level player in your team prevents you from receiving any experience." (category 110).</summary>
        public const int TeammateTooHighForXp = 121950320;

        public static void Send(Character character, string key)
            => Send(character, unchecked((int)ElfHash(key)));

        public static void Send(Character character, int messageId)
        {
            if (character is not Player player || player.Session == null)
                return;

            player.Session.Send(Create(player.Identity, messageId));
        }

        /// <summary>
        /// Category 110 feedback text the client formats itself. Retail quest kill progress (capture 2026-09-27):
        /// Unknown1 0, then "~&" + base-85 category + base-85 message id, then each argument as 'i' + base-85 int or
        /// 's' + (length + 1) + text.
        /// </summary>
        public static void SendFormatted(Character character, int messageId, params object[] args)
        {
            if (character is not Player player || player.Session == null)
                return;

            var text = new StringBuilder("~&").Append(Base85(CategoryId)).Append(Base85(messageId));
            foreach (object arg in args)
            {
                if (arg is int number)
                    text.Append('i').Append(Base85(number));
                else
                {
                    string value = arg?.ToString() ?? string.Empty;
                    if (value.Length > 254)
                        value = value[..254];
                    text.Append('s').Append((char)(value.Length + 1)).Append(value);
                }
            }

            player.Session.Send(new FormatFeedbackMessage
            {
                Identity = player.Identity,
                Unknown = 1,
                Unknown1 = 0,
                Unknown2 = 0,
                FormattedMessage = text.ToString()
            });
        }

        /// <summary>Five base-85 digits offset by '!', most significant first (110 -> "!!!\":").</summary>
        static string Base85(int value)
        {
            uint remaining = unchecked((uint)value);
            var digits = new char[5];
            for (int i = 4; i >= 0; i--)
            {
                digits[i] = (char)('!' + remaining % 85);
                remaining /= 85;
            }

            return new string(digits);
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
