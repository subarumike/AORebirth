namespace ZoneEngine_New.Core.Quests;

using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Quests.Dungeons;

public interface IMissionAcceptanceModule
{
    void Accept(CreateQuestMessage message, IZoneSession session);
}

internal sealed class MissionAcceptanceModule(QuestDungeonService dungeons) : IMissionAcceptanceModule
{
    public void Accept(CreateQuestMessage message, IZoneSession session)
    {
        if (session.State != SessionState.InPlay || session.Player is not { } player || !ReferenceEquals(player.Session, session)
            || player.IsDead || player.IsPersistenceQuarantined || message.Identity != player.Identity
            || message.QuestIdentity.Type != IdentityType.Mission)
            return;

        string? refusal = dungeons.AcceptOffer(player, message.QuestIdentity);
        if (refusal != null && !player.IsPersistenceQuarantined)
            session.Send(new ChatTextMessage { Identity = player.Identity, Text = refusal });
    }
}
