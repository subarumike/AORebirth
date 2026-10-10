namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Collections.Generic;
    using AORebirth.Interfaces.Persistence.Missions;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using Utility.GameData.Missions;
    using ZoneEngine.Core.Missions;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed class QuestAlternativeMessageHandler : IMessageHandler<QuestAlternativeMessage>
    {
        private readonly IGeneratedMissionDao _dao;
        private readonly GeneratedMissionService _missions;
        private readonly MissionDestinationCatalog _destinations;
        public QuestAlternativeMessageHandler(IGeneratedMissionDao dao, GeneratedMissionService missions, MissionDestinationCatalog destinations)
        { _dao = dao; _missions = missions; _destinations = destinations; }
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
                || dynel is not MissionTerminal terminal || terminal.GetEdgeDistanceTo(player) > LootableDynel.OpenRange)
                return;
            if (session is not IGameTimeSession { GameTimeSynchronizedAtUtc: { } synchronizedUtc })
            { Feedback(session, player, "The mission clock has not synchronized. No credits were deducted."); return; }
            int level = player.Stats.Get(CharacterStat.Level);
            var side = MissionLocationPool.ResolveCharacterSide(player.Stats.Get(CharacterStat.Side));
            if (!MissionLocationPool.CanCharacterRollAtTerminal(side, playfield.Identity.Instance))
            { Feedback(session, player, "This mission terminal is not available to your side. No credits were deducted."); return; }
            if (!MissionLevelRuntime.TryGetMissionQuality(level, message.LevelSlider, out int quality)
                || !MissionRollSliders.TryCreate(message, out _, out _))
            { Feedback(session, player, "The mission terminal rejected unsupported slider settings. No credits were deducted."); return; }
            int breed = player.Stats.Get(CharacterStat.Breed);
            int profession = player.Stats.Get(CharacterStat.Profession);
            _destinations.TryGetObservedDestinations(quality, out var destinationPool);
            player.Logger.Info(FormattableString.Invariant(
                $"Mission destination selection policy=OBSERVED_EXPECTED_QL_REUSE crossConditionEligibility=UNPROVEN owner={player.Identity.Instance} level={level} expectedQl={quality} difficulty={message.LevelSlider} faction={(int)side} breed={breed} profession={profession} terminalType={(uint)message.MissionTerminalIdentity.Type:X8} terminalInstance={unchecked((uint)message.MissionTerminalIdentity.Instance):X8} terminalPf={playfield.Identity.Instance} terminalX={terminal.Position.xf:R} terminalY={terminal.Position.yf:R} terminalZ={terminal.Position.zf:R} sliders=[{message.GoodBadSlider},{message.OrderChaosSlider},{message.OpenHiddenSlider},{message.PhysicalMysticalSlider},{message.HeadOnStealthSlider},{message.MoneyExperienceSlider}] physical={_destinations.Count} observed={_destinations.ObservedDestinations.Count} validWorldPos={_destinations.ObservedWorldPositionCount} expectedQlCandidates={destinationPool.Count}"));
            try
            {
                DateTime now = DateTime.UtcNow;
                int first;
                using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.roll.reserve-ids", player.Identity.Instance))
                    first = _dao.ReserveIdentities("offer", 5);
                int next = first;
                int seed = System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue);
                int nonce = System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue);
                QuestAlternativeMessage response;
                IReadOnlyList<MissionPlacementIdentity> selectedEntrances;
                GeneratedMissionOfferBatch batch;
                using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.roll.generate", player.Identity.Instance))
                {
                    try
                    {
                        response = GeneratedMissionRollService.Generate(message, player.Identity, level,
                            playfield.Identity.Instance, player.Position.xf, player.Position.zf,
                            side,
                            GeneratedMissionWire.ClientClock(synchronizedUtc, now), seed, nonce,
                            () => next < checked(first + 5) ? next++ : throw new InvalidOperationException("Mission identity reservation exhausted."),
                            _destinations, breed, profession, out selectedEntrances);
                    }
                    catch (NotSupportedException)
                    {
                        Feedback(session, player, $"No mission destinations have been observed at expected QL {quality}. No credits were deducted.");
                        return;
                    }
                    batch = GeneratedMissionRollProjection.Create(message, response, playfield.Identity.Instance,
                        MissionRollPolicy.Current.Fee(level), seed, nonce, now, selectedEntrances);
                }
                var committing = ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.roll.commit", player.Identity.Instance);
                var result = _missions.PublishOffers(player, batch);
                committing.Dispose();
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
