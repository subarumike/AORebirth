namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

    using Utility.Config;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    public sealed class ZoneLoginHandler
    {
        private readonly ICharacterHydrationService _hydration;
        private readonly PlayfieldManager _playfieldManager;
        private readonly IZoneLogger _logger;

        public ZoneLoginHandler(
            ICharacterHydrationService hydration,
            PlayfieldManager playfieldManager,
            IZoneLogger logger)
        {
            ArgumentNullException.ThrowIfNull(hydration);
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(logger);

            _hydration = hydration;
            _playfieldManager = playfieldManager;
            _logger = logger;
        }

        public void HandleAsync(MessageBody body, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(body);
            ArgumentNullException.ThrowIfNull(session);

            if (body is not ZoneLoginMessage message)
                return;

            _ = HandleAsyncCore(message, session);
        }

        private async Task HandleAsyncCore(ZoneLoginMessage message, IZoneSession session)
        {
            // EXPLOIT
            // TODO: Validate ZoneLoginMessage session cookies against the login handoff
            // before loading the character (reject mismatched/missing cookies).

            int characterId = message.CharacterId;
            if (characterId <= 0)
            {
                FailLogin(session, characterId, "Invalid character id.");
                return;
            }

            session.State = SessionState.Loading;

            if (_playfieldManager.FindPlayer(characterId, out Player existing))
            {
                BeginReconnect(session, existing, characterId);
                return;
            }

            CharacterHydrationResult? hydration = await LoadHydrationAsync(session, characterId).ConfigureAwait(false);
            if (hydration == null)
                return;

            Playfield playfield = _playfieldManager.GetOrCreate(hydration.Character.Playfield);

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
            SendGameTime(session, playfield, characterId);
            EnqueueSpawn(session, playfield, hydration);
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
            SendGameTime(session, playfield, characterId);

            playfield.TryEnqueue(
                new PendingReconnectInboundItem
                {
                    Session = session,
                    CharacterId = characterId
                });

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

        private static void SendGameTime(IZoneSession session, Playfield playfield, int characterId)
        {
            session.Send(
                new GameTimeMessage
                {
                    Identity = new Identity
                    {
                        Type = IdentityType.CanbeAffected,
                        Instance = characterId
                    },
                    Unknown1 = 30024.0f,
                    Unknown3 = 185408,
                    Unknown4 = 80183.3125f
                },
                playfield.Identity.Instance,
                characterId);
        }

        private static void EnqueueSpawn(
            IZoneSession session,
            Playfield playfield,
            CharacterHydrationResult hydration)
        {
            playfield.TryEnqueue(
                new PendingSpawnInboundItem
                {
                    Session = session,
                    Hydration = hydration
                });
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
