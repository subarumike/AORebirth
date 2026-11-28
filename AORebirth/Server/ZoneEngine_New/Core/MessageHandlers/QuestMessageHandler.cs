namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Quests;

public sealed class QuestMessageHandler(IGeneratedMissionLifecycleModule missions) : IMessageHandler<QuestMessage>
{
    public Type MessageBodyType => typeof(QuestMessage);
    public void Handle(MessageBody body, IZoneSession session) => Handle((QuestMessage)body, session);
    public void Handle(QuestMessage message, IZoneSession session)
    {
        var player = session.Player;
        if (session.State != SessionState.InPlay || player == null || !message.Identity.Equals(player.Identity)
            || message.Action != QuestAction.Delete || message.Mission.Type != IdentityType.Mission)
            return;
        missions.TryAbandon(player, message.Mission.Instance);
    }
}
