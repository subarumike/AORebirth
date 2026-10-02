namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Knubot;
using ZoneEngine_New.Core.Network;

/// <summary>Existing owner-tick dispatch; the service validates exact session, player, NPC and world.</summary>
public abstract class KnubotMessageHandler<T> : IMessageHandler<T> where T : MessageBody
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

public sealed class KnuBotOpenChatWindowMessageHandler(KnubotService knubot) : KnubotMessageHandler<KnuBotOpenChatWindowMessage>
{
    protected override void Dispatch(KnuBotOpenChatWindowMessage message, IZoneSession session) => knubot.TryOpen(session, message.Target);
}
public sealed class KnuBotAnswerMessageHandler(KnubotService knubot) : KnubotMessageHandler<KnuBotAnswerMessage>
{
    protected override void Dispatch(KnuBotAnswerMessage message, IZoneSession session) => knubot.TryAnswer(session, message.Target, message.Answer);
}
public sealed class KnuBotCloseChatWindowMessageHandler(KnubotService knubot) : KnubotMessageHandler<KnuBotCloseChatWindowMessage>
{
    protected override void Dispatch(KnuBotCloseChatWindowMessage message, IZoneSession session) => knubot.TryClose(session, message.Target);
}
public sealed class KnuBotStartTradeMessageHandler(KnubotService knubot) : KnubotMessageHandler<KnuBotStartTradeMessage>
{
    protected override void Dispatch(KnuBotStartTradeMessage message, IZoneSession session) => knubot.TryStartTrade(session, message.Target);
}
public sealed class KnuBotTradeMessageHandler(KnubotService knubot) : KnubotMessageHandler<KnuBotTradeMessage>
{
    protected override void Dispatch(KnuBotTradeMessage message, IZoneSession session) => knubot.TryTrade(session, message);
}
public sealed class KnuBotFinishTradeMessageHandler(KnubotService knubot) : KnubotMessageHandler<KnuBotFinishTradeMessage>
{
    protected override void Dispatch(KnuBotFinishTradeMessage message, IZoneSession session)
        => knubot.TryFinishTrade(session, message.Target, message.Decline != 0, message.Amount);
}
