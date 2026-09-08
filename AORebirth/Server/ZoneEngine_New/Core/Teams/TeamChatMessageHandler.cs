namespace ZoneEngine_New.Core.Teams
{
    using System;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.MessageHandlers;
    using ZoneEngine_New.Core.Network;

    /// <summary>The existing N3 /team and /invite route; never consumes ordinary vicinity text.</summary>
    public sealed class TeamChatMessageHandler(TeamService teams) : IMessageHandler<ChatCmdMessage>
    {
        public Type MessageBodyType => typeof(ChatCmdMessage);
        public void Handle(MessageBody body, IZoneSession session) => Handle((ChatCmdMessage)body, session);

        public void Handle(ChatCmdMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);
            Player? player = session.Player;
            if (session.State != SessionState.InPlay || player == null || !ReferenceEquals(player.Session, session)
                || message.Identity != player.Identity || player.IsPersistenceQuarantined) return;
            string text = ZoneEngine.Core.ChatCommandText.Normalize(message.Command);
            if (string.IsNullOrWhiteSpace(text)) return;
            string[] args = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args[0].Equals("team", StringComparison.OrdinalIgnoreCase))
                teams.TryHandleChatCommand(player, args);
            else if (args[0].Equals("invite", StringComparison.OrdinalIgnoreCase) && args.Length == 2)
                teams.TryHandleChatCommand(player, ["team", "invite", args[1]]);
            else
                player.Logger.Warn("Unsupported ChatCmd command=" + args[0]);
        }
    }
}
