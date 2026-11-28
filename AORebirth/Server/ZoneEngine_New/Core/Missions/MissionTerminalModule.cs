namespace ZoneEngine_New.Core.Missions
{
    using System;
    using System.Collections.Generic;
    using AORebirth.Interfaces.Persistence.Missions;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using Utility.GameData.Missions;
    using ZoneEngine.Core.Missions;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.GameData;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    internal interface IMissionTerminalModule
    {
        void Roll(QuestAlternativeMessage message, IZoneSession session);
    }

    internal sealed class MissionTerminalModule(
        IGeneratedMissionDao dao,
        GeneratedMissionService missions,
        MissionDestinationCatalog destinations,
        IGeneratedMissionOfferGenerator rolls,
        ContentSnapshot<MissionContent> content) : IMissionTerminalModule
    {
        private readonly MissionContent _content = content.Value;

        public void Roll(QuestAlternativeMessage message, IZoneSession session)
        {
            if (session.State != SessionState.InPlay || session.Player is not { } player
                || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined
                || player.Playfield is not { } playfield || message.Identity != player.Identity)
                return;
            if (message.QuestInfos?.Length > 0 || (int)message.MissionTerminalIdentity.Type != MissionTerminal.LiveIdentityType
                || !playfield.GetRequiredService<DynelRegistry>().TryGet(message.MissionTerminalIdentity, out var dynel)
                || dynel is not MissionTerminal terminal || terminal.GetEdgeDistanceTo(player) > LootableDynel.OpenRange)
                return;
            if (!_content.Policy.Enabled)
            { Feedback(session, player, "Mission terminals are disabled. No credits were deducted."); return; }
            if (session is not IGameTimeSession { GameTimeSynchronizedAtUtc: not null } clock)
            { Feedback(session, player, "The mission clock has not synchronized. No credits were deducted."); return; }
            int level = player.Stats.Get(CharacterStat.Level);
            var side = MissionLocationPool.ResolveCharacterSide(player.Stats.Get(CharacterStat.Side));
            if (!MissionLocationPool.CanCharacterRollAtTerminal(side, playfield.Identity.Instance))
            { Feedback(session, player, "This mission terminal is not available to your side. No credits were deducted."); return; }
            if (!MissionLevelRuntime.TryDecodeDifficultySlider(message.LevelSlider, out _)
                || !MissionRollSliders.TryCreate(message, out _, out _, _content.Policy))
            { Feedback(session, player, "The mission terminal rejected unsupported slider settings. No credits were deducted."); return; }
            int quality = _content.Levels.Quality(level, message.LevelSlider);
            int breed = player.Stats.Get(CharacterStat.Breed);
            int profession = player.Stats.Get(CharacterStat.Profession);
            bool supported = destinations.TryGetDestinations(playfield.Identity.Instance, quality,
                out var destinationPool, out bool usedQlFallback, out int selectedPoolQl);
            string destinationSelection = !supported ? "UNSUPPORTED" : selectedPoolQl != quality
                ? destinations.HasTerminalPools(playfield.Identity.Instance) ? "NEAREST_ORIGIN_QL_FALLBACK" : "NEAREST_QL_FALLBACK"
                : usedQlFallback ? "QL_FALLBACK" : "ORIGIN_SPECIFIC";
            player.Logger.Info(FormattableString.Invariant(
                $"Mission destination selection policy=TERMINAL_PLAYFIELD_EXPECTED_QL_WITH_QL_FALLBACK selection={destinationSelection} poolQl={selectedPoolQl} nearestTerminalQlFallback={destinations.AllowNearestTerminalQlFallback} crossConditionEligibility=UNPROVEN crossOriginEligibility=UNPROVEN crossQlEligibility=UNPROVEN owner={player.Identity.Instance} level={level} expectedQl={quality} difficulty={message.LevelSlider} faction={(int)side} breed={breed} profession={profession} terminalType={(uint)message.MissionTerminalIdentity.Type:X8} terminalInstance={unchecked((uint)message.MissionTerminalIdentity.Instance):X8} terminalPf={playfield.Identity.Instance} terminalX={terminal.Position.xf:R} terminalY={terminal.Position.yf:R} terminalZ={terminal.Position.zf:R} sliders=[{message.GoodBadSlider},{message.OrderChaosSlider},{message.OpenHiddenSlider},{message.PhysicalMysticalSlider},{message.HeadOnStealthSlider},{message.MoneyExperienceSlider}] physical={destinations.Count} observed={destinations.ObservedDestinations.Count} validWorldPos={destinations.ObservedWorldPositionCount} destinationCandidates={destinationPool.Count}"));
            try
            {
                DateTime now = DateTime.UtcNow;
                int first;
                using (ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.roll.reserve-ids", player.Identity.Instance))
                    first = dao.ReserveIdentities("offer", 5);
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
                        response = rolls.Roll(message, player.Identity, level, playfield.Identity.Instance,
                            GeneratedMissionWire.ClientClock(clock, now), seed, nonce,
                            () => next < checked(first + 5) ? next++ : throw new InvalidOperationException("Mission identity reservation exhausted."),
                            destinations, out selectedEntrances);
                    }
                    catch (NotSupportedException)
                    {
                        Feedback(session, player, $"No configured mission destinations are available at expected QL {quality}. No credits were deducted.");
                        return;
                    }
                    batch = GeneratedMissionRollProjection.Create(message, response, playfield.Identity.Instance,
                        _content.Policy.Fee(level), seed, nonce, now, selectedEntrances, _content.Policy);
                }
                var committing = ZoneEngine_New.Core.Metrics.TickStallWatch.Enter("mission.roll.commit", player.Identity.Instance);
                var result = missions.PublishOffers(player, batch);
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
