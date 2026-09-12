namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using AORebirth.Interfaces.Persistence.Missions;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine.Core.Missions;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed class QuestAlternativeMessageHandler : IMessageHandler<QuestAlternativeMessage>
    {
        private readonly IGeneratedMissionDao _dao;
        private readonly GeneratedMissionService _missions;
        public QuestAlternativeMessageHandler(IGeneratedMissionDao dao, GeneratedMissionService missions)
        { _dao = dao; _missions = missions; }
        public Type MessageBodyType => typeof(QuestAlternativeMessage);
        public void Handle(MessageBody body, IZoneSession session) => Handle((QuestAlternativeMessage)body, session);

        public void Handle(QuestAlternativeMessage message, IZoneSession session)
        {
            if (session.State != SessionState.InPlay || session.Player is not { } player
                || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined
                || player.Playfield is not { } playfield || message.Identity != player.Identity)
                return;
            if (message.QuestInfos?.Length > 0 || (int)message.MissionTerminalIdentity.Type != MissionTerminal.LiveIdentityType
                || !playfield.GetRequiredService<DynelRegistry>().TryGet(message.MissionTerminalIdentity, out var dynel)
                || dynel is not MissionTerminal terminal || terminal.Distance3D(player) > LootableDynel.OpenRange)
                return;
            if (session is not IGameTimeSession { GameTimeSynchronizedAtUtc: { } synchronizedUtc })
            { Feedback(session, player, "The mission clock has not synchronized. No credits were deducted."); return; }
            int level = player.Stats.Get(CharacterStat.Level);
            var side = MissionLocationPool.ResolveCharacterSide(player.Stats.Get(CharacterStat.Side));
            if (!MissionLocationPool.CanCharacterRollAtTerminal(side, playfield.Identity.Instance))
            { Feedback(session, player, "This mission terminal is not available to your side. No credits were deducted."); return; }
            if (!MissionLevelTable.TryGetMissionQuality(level, message.LevelSlider, out _)
                || !MissionSliderProfile.TryCreate(message, out _, out _))
            { Feedback(session, player, "The mission terminal rejected unsupported slider settings. No credits were deducted."); return; }
            try
            {
                DateTime now = DateTime.UtcNow;
                int first = _dao.ReserveIdentities("offer", 5);
                int next = first;
                var response = MissionRollService.BuildRollResponse(message, player.Identity, level,
                    playfield.Identity.Instance, player.Position.xf, player.Position.zf,
                    side,
                    MissionRollService.ResolveClientClockNowSeconds(synchronizedUtc, now), out int seed, out int nonce,
                    () => next < checked(first + 5) ? next++ : throw new InvalidOperationException("Mission identity reservation exhausted."));
                var batch = GeneratedMissionRollProjection.Create(message, response, playfield.Identity.Instance,
                    MissionRollFeeRules.FeeForLevel(level), seed, nonce, now);
                var result = _missions.PublishOffers(player, batch);
                if (result.Status == GeneratedMissionResultStatus.Applied)
                    session.Send(response);
                else if (!player.IsPersistenceQuarantined)
                    Feedback(session, player, "The mission roll was not committed. No new offers were issued.");
            }
            catch (MissionCommitOutcomeUnknownException)
            {
                player.QuarantinePersistence();
                session.Close();
            }
            catch (Exception exception)
            {
                player.Logger.Error(exception, "Mission roll failed; no uncommitted offer response was issued.");
                if (!player.IsPersistenceQuarantined)
                    Feedback(session, player, "The mission terminal could not issue the roll.");
            }
        }

        private static void Feedback(IZoneSession session, Player player, string text)
            => session.Send(new ChatTextMessage { Identity = player.Identity, Text = text });
    }
}
