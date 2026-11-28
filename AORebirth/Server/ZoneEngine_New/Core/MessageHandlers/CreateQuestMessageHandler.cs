namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Quests;

/// <summary>Accepting a terminal mission offer: it becomes a generated quest with an ACG dungeon.</summary>
public sealed class CreateQuestMessageHandler(IMissionAcceptanceModule missions) : IMessageHandler<CreateQuestMessage>
{
    public Type MessageBodyType => typeof(CreateQuestMessage);
    public void Handle(MessageBody body, IZoneSession session) => Handle((CreateQuestMessage)body, session);
    public void Handle(CreateQuestMessage message, IZoneSession session) => missions.Accept(message, session);
}
