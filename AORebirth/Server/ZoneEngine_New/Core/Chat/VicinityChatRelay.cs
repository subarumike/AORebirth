namespace ZoneEngine_New.Core.Chat
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Communication.Messages;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Playfield;

    public sealed class VicinityChatRelay
    {
        private readonly IChatEngineLink _chatLink;
        private readonly IZoneLogger _logger;

        public VicinityChatRelay(IChatEngineLink chatLink, IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(chatLink);
            ArgumentNullException.ThrowIfNull(logger);
            _chatLink = chatLink;
            _logger = logger;
        }

        public void Relay(Player speaker, TextMessage message)
        {
            ArgumentNullException.ThrowIfNull(speaker);
            ArgumentNullException.ThrowIfNull(message);

            ChatMessage? chat = message.Message;
            if (chat == null || string.IsNullOrEmpty(chat.Text))
                return;

            Playfield? playfield = speaker.Playfield;
            if (playfield == null)
                return;

            if (!TryResolveRange(chat.Type, out float range))
                return;

            List<int> recipientIds = new List<int>();
            int senderId = speaker.Identity.Instance;
            recipientIds.Add(senderId);

            DynelRegistry registry = playfield.GetRequiredService<DynelRegistry>();
            foreach (Player other in registry.PlayerEntities())
            {
                if (other.Identity.Instance == senderId)
                    continue;
                if (speaker.Distance3D(other) > range)
                    continue;
                recipientIds.Add(other.Identity.Instance);
            }

            VicinityChatMessage vicinity = new VicinityChatMessage
            {
                CharacterIds = recipientIds,
                MessageType = (byte)chat.Type,
                Text = chat.Text,
                SenderId = senderId
            };

            if (_chatLink.TrySend(vicinity))
                return;

            _logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Vicinity chat send failed character={0} type={1} recipients={2}",
                    senderId,
                    chat.Type,
                    recipientIds.Count));
        }

        private static bool TryResolveRange(ChatMessageType type, out float range)
        {
            switch (type)
            {
                case ChatMessageType.Whisper:
                    range = 1.5f;
                    return true;
                case ChatMessageType.Say:
                    range = 10.0f;
                    return true;
                case ChatMessageType.Shout:
                    range = 60.0f;
                    return true;
                default:
                    range = 0;
                    return false;
            }
        }
    }
}
