namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;

public sealed class QuestMessageHandler(GeneratedMissionAcgService missions) : IMessageHandler<QuestMessage>
{
    public Type MessageBodyType => typeof(QuestMessage);
    public void Handle(MessageBody body, IZoneSession session) => Handle((QuestMessage)body, session);
    public void Handle(QuestMessage message, IZoneSession session)
    {
        var player = session.Player;
        if (session.State != SessionState.InPlay || player == null || player.IsPersistenceQuarantined || player.IsDead
            || !message.Identity.Equals(player.Identity) || message.Action != QuestAction.Delete || (int)message.Mission.Type != 0xDAC3) return;
        missions.Abandon(player, message.Mission);
    }
}
