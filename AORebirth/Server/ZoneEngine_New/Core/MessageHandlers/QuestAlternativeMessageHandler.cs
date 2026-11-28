namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Missions;
using ZoneEngine_New.Core.Network;

internal sealed class QuestAlternativeMessageHandler(IMissionTerminalModule missions) : IMessageHandler<QuestAlternativeMessage>
{
    public Type MessageBodyType => typeof(QuestAlternativeMessage);

    public void Handle(MessageBody body, IZoneSession session) => Handle((QuestAlternativeMessage)body, session);

    public void Handle(QuestAlternativeMessage message, IZoneSession session) => missions.Roll(message, session);
}
