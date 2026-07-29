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
    /// Capture 20260729-010949: N3 TeamInvite popup to cross-zone invitee.
    /// Wire: Identity=invitee, Unknown=1, Inviter, NameLen(Int16), inviter name.
    /// </summary>
    [MessageHandler(MessageHandlerDirection.OutboundOnly)]
    public class TeamInviteMessageHandler : BaseMessageHandler<TeamInviteMessage, TeamInviteMessageHandler>
    {
        public void Send(ICharacter invitee, ICharacter inviter)
        {
            if (invitee == null || inviter == null)
            {
                return;
            }

            Identity inviterId = new Identity
            {
                Type = IdentityType.CanbeAffected,
                Instance = inviter.Identity.Instance
            };

            this.Send(
                invitee,
                x =>
                {
                    x.Identity = invitee.Identity;
                    x.Unknown = 1;
                    x.Inviter = inviterId;
                    x.Name = inviter.Name ?? string.Empty;
                },
                false);
        }
    }
}
