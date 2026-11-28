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
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Quests;

    public sealed class ZoneLoginHandler
    {
        private readonly ICharacterHydrationService _hydration;
        private readonly PlayfieldManager _playfieldManager;
        private readonly IZoneLogger _logger;
        private readonly IZoneAdmissionGate _admission;
        private readonly ZoneEngine_New.Core.World.ZoneRedirects _redirects;
        private readonly ZoneEngine_New.Core.World.WorldLink? _world;
        private readonly ICharacterRepository? _characters;
        private readonly ZoneLoginThrottle _throttle;
        private readonly IGeneratedMissionLifecycleModule _missions;

        public ZoneLoginHandler(
            ICharacterHydrationService hydration,
            PlayfieldManager playfieldManager,
            IZoneLogger logger,
            IGeneratedMissionLifecycleModule missions,
            IZoneAdmissionGate admission,
            ZoneEngine_New.Core.World.ZoneRedirects redirects,
            ZoneEngine_New.Core.World.WorldLink? world = null,
            ICharacterRepository? characters = null,
            ZoneLoginThrottle? throttle = null)
        {
            _throttle = throttle ?? new ZoneLoginThrottle();
            _world = world;
            _characters = characters;
            _redirects = redirects ?? throw new ArgumentNullException(nameof(redirects));
            _missions = missions ?? throw new ArgumentNullException(nameof(missions));
            ArgumentNullException.ThrowIfNull(hydration);
            ArgumentNullException.ThrowIfNull(playfieldManager);
            ArgumentNullException.ThrowIfNull(logger);

            _hydration = hydration;
            _playfieldManager = playfieldManager;
            _logger = logger;
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
            if (session is not ZoneSession zoneSession)
            {
                _logger.Warn("Zone admission rejected: reason=unsupported_session");
                session.Close();
                return;
            }

            System.Net.IPAddress? remote = zoneSession.RemoteAddress();
            if (_throttle.IsBlocked(remote))
            {
                session.Close();
                return;
            }

            ZoneAdmission claim;
            try { claim = await _admission.ClaimAsync(message).ConfigureAwait(false); }
            catch (Exception exception)
            {
                _logger.Warn("Zone admission rejected: reason=authority_unavailable (" + exception.GetType().Name + ")");
                session.Close();
                return;
            }
            if (!claim.Accepted)
            {
                _logger.Warn("Zone admission rejected: reason=" + claim.Reason + " from=" + zoneSession.RemoteEndPoint());
                if (_throttle.RecordFailure(remote))
                    _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                        "Zone admissions from {0} refused for a while after {1} rejected attempts", remote, ZoneLoginThrottle.MaxFailures));
                session.Close();
                return;
            }

            _throttle.RecordSuccess(remote);

            zoneSession.BindZoneHandoff(message.CharacterId, message.Cookie1, message.Cookie2, _redirects, claim.Account);

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
            _missions.PrepareLogin(characterId);

            CharacterHydrationResult? hydration = await LoadHydrationAsync(session, characterId).ConfigureAwait(false);
            if (hydration == null || session.State != SessionState.Loading)
                return;

            IDisposable? playfieldAdmission = null;
            try
            {
                Playfield playfield;
                int storedPlayfield = hydration.Character.Playfield;
                if (_missions.ResolveLogin(characterId, storedPlayfield) is { } missionDestination)
                {
                    playfieldAdmission = missionDestination.Admission;
                    if (missionDestination.Playfield is not Playfield missionPlayfield)
                    {
                        FailLogin(session, characterId, "Saved inside a retired mission world with no way out.");
                        return;
                    }

                    playfield = missionPlayfield;
                    if (missionDestination.Position is { } landing)
                        hydration = WithLoginPosition(hydration, playfield.Identity.Instance, landing);
                }
                else
                {
                    // Saved inside a proxy instance whose door no longer leads to an interior: come back out in front of
                    // the source playfield's door instead of failing the login.
                    if (ProxyPlayfieldIds.IsProxy(storedPlayfield)
                        && !_playfieldManager.TryResolveProxyResource(storedPlayfield, out _, out int sourcePlayfield)
                        && sourcePlayfield > 0)
                    {
                        _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                            "Character {0} saved in proxy playfield {1} with no interior behind it; logging in on playfield {2}",
                            characterId, storedPlayfield, sourcePlayfield));
                        storedPlayfield = sourcePlayfield;
                    }

                    var ordinaryAdmission = _playfieldManager.AcquirePlayfield(storedPlayfield);
                    playfieldAdmission = ordinaryAdmission;
                    playfield = ordinaryAdmission.Playfield;
                }

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
                EnqueueSpawn(session, playfield, hydration, claim.Kind == ZoneAdmissionKind.Transfer ? claim.Envelope : null);
            }
            catch (ZoneEngine_New.Core.World.LoginRelocationException relocation)
            {
                Relocate(session, hydration, relocation.PlayfieldId, relocation.Position);
            }
            catch (ZoneEngine_New.Core.World.PlayfieldHostedElsewhereException elsewhere)
            {
                // World moved the stored playfield to another instance after routing this login: log in again.
                Relocate(session, hydration, elsewhere.PlayfieldId,
                    new AORebirth.Core.Vector.Vector3(hydration.Character.X, hydration.Character.Y, hydration.Character.Z));
            }
            finally
            {
                playfieldAdmission?.Dispose();
            }
        }

        /// <summary>
        /// The login has to continue on another zone instance: store where the character goes, give the lease back,
        /// and close; the next login is routed to the instance that runs that playfield.
        /// </summary>
        private void Relocate(IZoneSession session, CharacterHydrationResult hydration, int playfieldId,
            AORebirth.Core.Vector.Vector3 position)
        {
            CharacterRecord stored = hydration.Character;
            int characterId = stored.Id;
            try
            {
                _characters?.SaveLocation(new CharacterRecord
                {
                    Id = characterId,
                    Name = stored.Name,
                    FirstName = stored.FirstName,
                    LastName = stored.LastName,
                    Playfield = playfieldId,
                    X = position.xf,
                    Y = position.yf,
                    Z = position.zf,
                    HeadingX = stored.HeadingX,
                    HeadingY = stored.HeadingY,
                    HeadingZ = stored.HeadingZ,
                    HeadingW = stored.HeadingW
                }, online: 0);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Login relocation save failed for character " + characterId.ToString(CultureInfo.InvariantCulture));
            }

            _world?.Abandon(characterId);
            _logger.Warn(string.Format(CultureInfo.InvariantCulture,
                "ZoneLogin character={0} relocates to playfield {1} on another zone instance; the client logs in again",
                characterId, playfieldId));
            session.Close();
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
                    HeadingX = source.HeadingX, HeadingY = source.HeadingY, HeadingZ = source.HeadingZ, HeadingW = source.HeadingW,
                    AccountFlags = source.AccountFlags, GmLevel = source.GmLevel
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

            using PlayfieldAdmission? playfieldAdmission = playfield.TryAcquireAdmission();
            if (playfieldAdmission == null)
            {
                FailLogin(session, characterId, "In-world playfield is no longer accepting arrivals.");
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
                playfield.ClientPlayfieldId,
                characterId);
        }

        private static void EnqueueSpawn(
            IZoneSession session,
            Playfield playfield,
            CharacterHydrationResult hydration,
            AORebirth.World.Contracts.V1.TransferEnvelope? envelope)
        {
            if (!playfield.TryEnqueue(
                new PendingSpawnInboundItem
                {
                    Session = session,
                    Hydration = hydration,
                    Envelope = envelope
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
