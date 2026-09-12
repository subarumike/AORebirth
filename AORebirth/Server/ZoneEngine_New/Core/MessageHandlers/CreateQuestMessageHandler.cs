namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;

public sealed class CreateQuestMessageHandler(GeneratedMissionAcgService missions) : IMessageHandler<CreateQuestMessage>
{
    public Type MessageBodyType => typeof(CreateQuestMessage);
    public void Handle(MessageBody body, IZoneSession session) => Handle((CreateQuestMessage)body, session);
    public void Handle(CreateQuestMessage message, IZoneSession session)
    {
        if (session.State != SessionState.InPlay || session.Player is not { } player || !ReferenceEquals(player.Session, session)
            || player.IsDead || player.IsPersistenceQuarantined || message.Identity != player.Identity || (int)message.QuestIdentity.Type != 0xDAC3)
            return;
        var result = missions.Accept(player, message.QuestIdentity);
        if (result.Status == AORebirth.Interfaces.Persistence.Missions.GeneratedMissionResultStatus.Rejected && !player.IsPersistenceQuarantined)
            session.Send(new ChatTextMessage { Identity = player.Identity, Text = result.Reason });
    }
}
