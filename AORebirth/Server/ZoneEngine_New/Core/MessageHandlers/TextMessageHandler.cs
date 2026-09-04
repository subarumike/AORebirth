namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.Messages;

    using ZoneEngine_New.Core.Chat;
    using ZoneEngine_New.Core.Commands;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    public sealed class TextMessageHandler : IMessageHandler<TextMessage>
    {
        private readonly GmCommandDispatcher _commands;
        private readonly VicinityChatRelay _vicinity;

        public TextMessageHandler(GmCommandDispatcher commands, VicinityChatRelay vicinity)
        {
            ArgumentNullException.ThrowIfNull(commands);
            ArgumentNullException.ThrowIfNull(vicinity);
            _commands = commands;
            _vicinity = vicinity;
        }

        public Type MessageBodyType => typeof(TextMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((TextMessage)body, session);
        }

        public void Handle(TextMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null)
                return;

            string? text = message.Message?.Text;
            if (string.IsNullOrEmpty(text))
                return;

            if (text[0] != '.')
            {
                _vicinity.Relay(player, message);
                return;
            }

            string trimmed = text.TrimStart('.');
            if (string.IsNullOrWhiteSpace(trimmed))
                return;

            string[] parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            string name = parts[0];
            string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            _commands.TryExecute(session, player, name, args);
        }
    }
}
