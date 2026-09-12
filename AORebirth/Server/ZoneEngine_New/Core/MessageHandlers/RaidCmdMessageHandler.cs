namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Teams;

    public sealed class RaidCmdMessageHandler : IMessageHandler<RaidCmdMessage>
    {
        private readonly TeamService _teams;
        public RaidCmdMessageHandler(TeamService teams) => _teams = teams;
        public Type MessageBodyType => typeof(RaidCmdMessage);
        public void Handle(MessageBody body, IZoneSession session) => Handle((RaidCmdMessage)body, session);
        public void Handle(RaidCmdMessage message, IZoneSession session)
        {
            if (session.State != SessionState.InPlay || session.Player is not { } player
                || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined)
                return;
            // Existing accepted RaidCmd capture proves only command 1.
            if (message.Command == 1) _teams.ConvertToRaid(player);
        }
    }
}
