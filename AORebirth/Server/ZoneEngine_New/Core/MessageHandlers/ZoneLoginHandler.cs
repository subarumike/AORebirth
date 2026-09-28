namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Globalization;
    using System.Linq;
    using AORebirth.Interfaces.Persistence.Missions;
    using System.Threading.Tasks;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

    using Utility.Config;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine.Core.Missions;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed class ZoneLoginHandler
    {
        private readonly ICharacterHydrationService _hydration;
        private readonly PlayfieldManager _playfieldManager;
        private readonly IZoneLogger _logger;
        private readonly IGeneratedMissionDao _retiredMissions;
        private readonly IZoneAdmissionGate _admission;
        private readonly Quests.Dungeons.QuestDungeonService? _dungeons;

        public ZoneLoginHandler(
            ICharacterHydrationService hydration,
            PlayfieldManager playfieldManager,
            IZoneLogger logger,
            IGeneratedMissionDao retiredMissions,
            IZoneAdmissionGate admission,
            Quests.Dungeons.QuestDungeonService? dungeons = null)
        {
            _dungeons = dungeons;
            ArgumentNullException.ThrowIfNull(hydration);
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(logger);

            _hydration = hydration;
            _playfieldManager = playfieldManager;
            _logger = logger;
            _retiredMissions = retiredMissions ?? throw new ArgumentNullException(nameof(retiredMissions));
            _admission = admission ?? throw new ArgumentNullException(nameof(admission));
        }

        public void HandleAsync(MessageBody body, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(session);

            if (body is not ZoneLoginMessage message)
                return;

            lock (session)
            {
                if (session.State != SessionState.Connected)
                {
                    session.Close();
                    return;
                }
                session.State = SessionState.Loading;
            }
            _ = HandleAsyncCore(message, session);
        }

        private async Task HandleAsyncCore(ZoneLoginMessage message, IZoneSession session)
        {
            try
            {
                await HandleAsyncCoreInner(message, session).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "ZoneLogin failed with unhandled exception.");
                session.Close();
            }
        }

        private async Task HandleAsyncCoreInner(ZoneLoginMessage message, IZoneSession session)
        {
            AORebirth.Database.Dao.ZoneHandoffClaim claim;
            try { claim = _admission.Claim(message); }
            catch
            {
                _logger.Warn("Zone admission rejected: reason=authority_unavailable");
                session.Close();
                return;
            }
            if (!claim.Accepted)
            {
                _logger.Warn("Zone admission rejected: reason=" + claim.Reason);
                session.Close();
                return;
            }

            if (session is not ZoneSession zoneSession)
            {
                _logger.Warn("Zone admission rejected: reason=unsupported_session");
                session.Close();
                return;
            }
            zoneSession.BindZoneHandoff(message.CharacterId, message.Cookie1, message.Cookie2);

            int characterId = message.CharacterId;
            if (characterId <= 0)
            {
                FailLogin(session, characterId, "Invalid character id.");
                return;
            }

            if (_playfieldManager.FindPlayer(characterId, out Player existing))
            {
                BeginReconnect(session, existing, characterId);
                return;
            }

            // Keys whose dungeon ended while this character was away are retired before their items load.
            _dungeons?.RetireDeadKeysOnLogin(characterId);

            CharacterHydrationResult? hydration = await LoadHydrationAsync(session, characterId).ConfigureAwait(false);
            if (hydration == null || session.State != SessionState.Loading)
                return;

            Playfield playfield;
            int storedPlayfield = hydration.Character.Playfield;
            if (_dungeons != null && Quests.Dungeons.QuestDungeonIds.IsDungeonPlayfield(storedPlayfield))
            {
                (Playfield dungeonOrExterior, AORebirth.Core.Vector.Vector3? landing) = _dungeons.ResolveLogin(storedPlayfield);
                playfield = dungeonOrExterior;
                // Vector3 overloads != with v1.Equals, which throws for a null left operand.
                if (landing is not null)
                    hydration = WithLoginPosition(hydration, dungeonOrExterior.Identity.Instance, landing);
            }
            else if (storedPlayfield >= GeneratedMissionIdentitySpace.MinimumLivePlayfield2
                && storedPlayfield <= GeneratedMissionIdentitySpace.MaximumLivePlayfield2)
            {
                // The old mission worlds are gone: a character saved inside one returns to where that mission was
                // accepted (its offer's destination).
                GeneratedMissionBinding? binding = _retiredMissions.ReadAccepted(characterId)
                    .FirstOrDefault(value => value.LivePlayfield == storedPlayfield);
                if (binding?.Offer == null || binding.Offer.DestinationPlayfield <= 0)
                {
                    FailLogin(session, characterId, "Saved inside a retired mission world with no way out.");
                    return;
                }

                playfield = _playfieldManager.GetOrCreate(binding.Offer.DestinationPlayfield);
                hydration = WithLoginPosition(hydration, binding.Offer.DestinationPlayfield,
                    new AORebirth.Core.Vector.Vector3(binding.Offer.DestinationX, binding.Offer.DestinationY, binding.Offer.DestinationZ));
            }
            else playfield = _playfieldManager.GetOrCreate(storedPlayfield);

            session.SendInitiateCompression();
            SendChatServerInfo(session, playfield, characterId);
            SendPlayfieldAnarchyF(
                session,
                playfield,
                new Vector3
                {
                    X = hydration.Character.X,
                    Y = hydration.Character.Y,
                    Z = hydration.Character.Z
                },
                characterId);
            EnqueueSpawn(session, playfield, hydration);
        }

        /// <summary>The same hydration, placed at <paramref name="position"/> on <paramref name="playfieldId"/>.</summary>
        static CharacterHydrationResult WithLoginPosition(CharacterHydrationResult hydration, int playfieldId, AORebirth.Core.Vector.Vector3 position)
        {
            var source = hydration.Character;
            return new CharacterHydrationResult
            {
                Character = new CharacterRecord
                {
                    Id = source.Id, Name = source.Name, FirstName = source.FirstName, LastName = source.LastName,
                    Playfield = playfieldId, X = position.xf, Y = position.yf, Z = position.zf,
                    HeadingX = source.HeadingX, HeadingY = source.HeadingY, HeadingZ = source.HeadingZ, HeadingW = source.HeadingW
                },
                Stats = hydration.Stats, Items = hydration.Items, UploadedNanoIds = hydration.UploadedNanoIds
            };
        }

        private void BeginReconnect(IZoneSession session, Player player, int characterId)
        {
            if (player.Playfield is not Playfield playfield)
            {
                FailLogin(session, characterId, "In-world player has no playfield.");
                return;
            }

            AORebirth.Core.Vector.Vector3 position = player.Position;

            session.SendInitiateCompression();
            SendChatServerInfo(session, playfield, characterId);
            SendPlayfieldAnarchyF(
                session,
                playfield,
                new Vector3
                {
                    X = position.xf,
                    Y = position.yf,
                    Z = position.zf
                },
                characterId);
            if (!playfield.TryEnqueue(
                new PendingReconnectInboundItem
                {
                    Session = session,
                    CharacterId = characterId
                }))
            {
                session.Close();
                return;
            }

            _logger.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneLogin reconnect enqueued character={0} playfield={1} phase={2}",
                    characterId,
                    playfield.Identity.Instance,
                    player.ConnectionPhase));
        }

        private async Task<CharacterHydrationResult?> LoadHydrationAsync(IZoneSession session, int characterId)
        {
            CharacterHydrationResult? hydration;
            try
            {
                hydration = await Task.Run(() => _hydration.LoadForLogin(characterId)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Character hydration failed during login.");
                FailLogin(session, characterId, "Character hydration failed.");
                return null;
            }

            if (hydration == null || !hydration.IsSpawnReady)
            {
                FailLogin(
                    session,
                    characterId,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Character {0} not ready for spawn.",
                        characterId));
                return null;
            }

            return hydration;
        }

        private static void SendChatServerInfo(IZoneSession session, Playfield playfield, int characterId)
        {
            Config config = ConfigReadWrite.Instance.CurrentConfig;
            session.Send(
                new ChatServerInfoMessage
                {
                    HostName = string.IsNullOrWhiteSpace(config.ChatIP) ? "127.0.0.1" : config.ChatIP,
                    Port = config.ChatPort > 0 ? config.ChatPort : 7012
                },
                playfield.Identity.Instance,
                characterId);
        }

        private static void SendPlayfieldAnarchyF(
            IZoneSession session,
            Playfield playfield,
            Vector3 characterCoordinates,
            int characterId)
        {
            session.Send(
                playfield.CreatePlayfieldAnarchyFMessage(characterCoordinates),
                playfield.Identity.Instance,
                characterId);
        }

        private static void EnqueueSpawn(
            IZoneSession session,
            Playfield playfield,
            CharacterHydrationResult hydration)
        {
            if (!playfield.TryEnqueue(
                new PendingSpawnInboundItem
                {
                    Session = session,
                    Hydration = hydration
                })) session.Close();
        }

        private void FailLogin(IZoneSession session, int characterId, string error)
        {
            _logger.Warn(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ZoneLogin failed character={0}: {1}",
                    characterId,
                    error));

            session.Close();
        }
    }
}
