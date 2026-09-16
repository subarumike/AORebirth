namespace ZoneEngine_New.Core.MessageHandlers;

using System;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Network;

// No DAO, inventory or world service is reachable from these unsupported handlers.
public static class UnavailableGameplay
{
    public static void Reject(IZoneSession session, string feature)
    {
        if (session.State != SessionState.InPlay || session.Player is not { } player
            || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined) return;
        session.Send(new ChatTextMessage { Identity = player.Identity,
            Text = feature + " is unavailable in this build. No state was changed." });
    }
}

public abstract class UnavailableHandler<T>(string feature) : IMessageHandler<T> where T : MessageBody
{
    public Type MessageBodyType => typeof(T);
    public void Handle(MessageBody body, IZoneSession session) => Handle((T)body, session);
    public void Handle(T message, IZoneSession session) => UnavailableGameplay.Reject(session, feature);
}

public sealed class QuestAlternativeMessageHandler() : UnavailableHandler<QuestAlternativeMessage>("Mission offers");
public sealed class CreateQuestMessageHandler() : UnavailableHandler<CreateQuestMessage>("Mission acceptance");
public sealed class QuestMessageHandler() : UnavailableHandler<QuestMessage>("Mission actions");
public sealed class KnuBotOpenChatWindowMessageHandler() : UnavailableHandler<KnuBotOpenChatWindowMessage>("Dialogue");
public sealed class KnuBotAnswerMessageHandler() : UnavailableHandler<KnuBotAnswerMessage>("Dialogue");
public sealed class KnuBotCloseChatWindowMessageHandler() : UnavailableHandler<KnuBotCloseChatWindowMessage>("Dialogue");
public sealed class KnuBotTradeMessageHandler() : UnavailableHandler<KnuBotTradeMessage>("Dialogue trade");
public sealed class KnuBotFinishTradeMessageHandler() : UnavailableHandler<KnuBotFinishTradeMessage>("Dialogue trade");
