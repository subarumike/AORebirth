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
    /// Capture 20260727-065826: TeamMemberInfo after roster announce.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class TeamMemberInfoMessageHandler : BaseMessageHandler<TeamMemberInfoMessage, TeamMemberInfoMessageHandler>
    {
        public void Send(ICharacter viewer, Identity memberIdentity, int unknown3, int unknown5)
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
                    x.Unknown3 = unknown3;
                    x.Unknown4 = unknown3;
                    x.Unknown5 = unknown5;
                    x.Unknown6 = unknown5;
                },
                false);
        }
    }
}
