namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Dialogue;
using ZoneEngine_New.Core.Network;

/// <summary>Existing owner-tick dispatch; the service validates exact session, player, NPC and world.</summary>
public abstract class DialogueMessageHandler<T> : IMessageHandler<T> where T : MessageBody
{
    public Type MessageBodyType => typeof(T);
    public void Handle(MessageBody body, IZoneSession session) => Handle((T)body, session);
    public void Handle(T message, IZoneSession session)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(session);
        if (session.State == SessionState.InPlay) Dispatch(message, session);
    }
    protected abstract void Dispatch(T message, IZoneSession session);
}

public sealed class KnuBotOpenChatWindowMessageHandler(DialogueService service) : DialogueMessageHandler<KnuBotOpenChatWindowMessage>
{
    protected override void Dispatch(KnuBotOpenChatWindowMessage message, IZoneSession session) => service.Open(session, message.Target);
}
public sealed class KnuBotAnswerMessageHandler(DialogueService service) : DialogueMessageHandler<KnuBotAnswerMessage>
{
    protected override void Dispatch(KnuBotAnswerMessage message, IZoneSession session) => service.Answer(session, message.Target, message.Answer);
}
public sealed class KnuBotCloseChatWindowMessageHandler(DialogueService service) : DialogueMessageHandler<KnuBotCloseChatWindowMessage>
{
    protected override void Dispatch(KnuBotCloseChatWindowMessage message, IZoneSession session) => service.Close(session, message.Target);
}
public sealed class KnuBotTradeMessageHandler(DialogueService service) : DialogueMessageHandler<KnuBotTradeMessage>
{
    protected override void Dispatch(KnuBotTradeMessage message, IZoneSession session) => service.StageTrade(session, message);
}
public sealed class KnuBotFinishTradeMessageHandler(DialogueService service) : DialogueMessageHandler<KnuBotFinishTradeMessage>
{
    protected override void Dispatch(KnuBotFinishTradeMessage message, IZoneSession session) => service.FinishTrade(session, message);
}
