namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// Capture 20260727-065826: TeamMember roster announce.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class TeamMemberMessageHandler : BaseMessageHandler<TeamMemberMessage, TeamMemberMessageHandler>
    {
        public void Send(
            ICharacter viewer,
            Identity memberIdentity,
            Identity teamIdentity,
            string memberName,
            int memberLevel,
            short unknown5)
        {
            if (viewer == null)
            {
                return;
            }

            this.Send(
                viewer,
                x =>
                {
                    x.Identity = viewer.Identity;
                    x.Unknown = 0;
                    x.Member = memberIdentity;
                    x.Team = teamIdentity;
                    x.Unknown4 = -1;
                    x.Level = memberLevel;
                    x.Unknown5 = unknown5;
                    x.Name = memberName ?? string.Empty;
                },
                false);
        }
    }
}
