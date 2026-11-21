namespace ZoneEngine_New.Core.World
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using AORebirth.World.Contracts.V1;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Trade;

    using PlayfieldInstance = ZoneEngine_New.Core.Playfield.Playfield;
    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// A move to a playfield another zone instance runs: save here (as a logout does, with the character placed at
    /// the destination), let World move the lease, then redirect the client. The destination reloads the character
    /// from the database and applies the envelope (pets, the arrival's door). Until World accepts, nothing is lost:
    /// a refusal leaves the player where it was.
    /// </summary>
    public sealed class CrossInstanceTransfer
    {
        readonly WorldLink _world;
        readonly CharacterSnapshotService _snapshot;
        readonly InventoryFlushService _flush;
        readonly TradeService _trades;
        readonly IZoneLogger _logger;
        readonly ZoneEngine_New.Core.GameData.IGameData _gameData;

        public CrossInstanceTransfer(WorldLink world, CharacterSnapshotService snapshot, InventoryFlushService flush,
            TradeService trades, IZoneLogger logger, ZoneEngine_New.Core.GameData.IGameData gameData)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _flush = flush ?? throw new ArgumentNullException(nameof(flush));
            _trades = trades ?? throw new ArgumentNullException(nameof(trades));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        }

        /// <summary>Runs on the player's own playfield tick. False when the move could not start.</summary>
        public bool Start(Player player, int destinationPlayfield, Vector3 landing, Quaternion heading,
            ArrivalKind arrival = ArrivalKind.ArrivalZone, int exitProxyDoor = 0, bool clearReturnDoor = false)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(landing);
            ArgumentNullException.ThrowIfNull(heading);
            if (player.Playfield is not PlayfieldInstance source || player.Session is not ZoneSession session)
                return false;
            int characterId = player.Identity.Instance;
            if (!session.TryGetHandoffCookies(characterId, out uint cookie1, out uint cookie2) || !_world.IsServing)
            {
                Tell(player, "That destination is not available right now.");
                return false;
            }

            if (!session.TryBeginRemoteTransfer(player))
                return false;

            TransferEnvelope envelope;
            try
            {
                envelope = BuildEnvelope(player, destinationPlayfield, landing, heading, arrival, exitProxyDoor, clearReturnDoor);
                if (source.GetRequiredService<PlayfieldManager>().Teams.SnapshotTeamState(player) is { } team)
                    envelope.Team = team;
                // The logout save, in the same order (SpawnService.DespawnPlayer).
                player.InterruptTimedActions(TimedActionInterrupt.LeavePlayfield);
                // Offered items go back before the save, as on logout.
                _trades.Cancel(player, "changed zone");
                if (player.Inventory.IsHydrated)
                {
                    player.SnapshotActiveNanosForTransfer();
                    _flush.HardFlush(player);
                }

                _snapshot.CommitForTransfer(player, destinationPlayfield, landing, heading);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Transfer save failed for character " + characterId.ToString(CultureInfo.InvariantCulture));
                Restore(player, session, "Your zone change failed.");
                return false;
            }

            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Cross-instance transfer character={0} from={1} to={2}", characterId, source.Identity.Instance, destinationPlayfield));

            // World answers off the tick; the outcome is applied back on the player's own tick.
            _ = Task.Run(async () =>
            {
                // Null: the outcome is unknown (timeout, dropped connection). World may have moved the lease.
                TransferReply? reply;
                try
                {
                    reply = await _world.BeginTransferAsync(characterId, cookie1, cookie2, envelope).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.Error("Transfer of character " + characterId.ToString(CultureInfo.InvariantCulture)
                        + ": no answer from World (" + exception.GetType().Name + ")");
                    reply = null;
                }

                source.DispatchPlayerProjection(player,
                    () => Complete(player, session, source, reply, landing, heading, destinationPlayfield, clearReturnDoor));
            });
            return true;
        }

        void Complete(Player player, ZoneSession session, PlayfieldInstance source, TransferReply? reply,
            Vector3 landing, Quaternion heading, int destinationPlayfield, bool clearReturnDoor)
        {
            int characterId = player.Identity.Instance;
            if (reply == null)
            {
                // Never keep playing a character World might have handed on: two live copies could each save it.
                // It was saved for the move, so it simply logs in again (World routes it to the right instance).
                Tell(player, "Your zone change could not be confirmed. Please log in again.");
                if (ReferenceEquals(player.Playfield, source))
                    source.GetRequiredService<SpawnService>().DepartToInstance(player);
                session.Close();
                return;
            }

            if (!reply.Accepted)
            {
                _logger.Warn("Cross-instance transfer of character " + characterId.ToString(CultureInfo.InvariantCulture)
                    + " refused: " + reply.Reason);
                Restore(player, session, "That destination is not available right now.");
                return;
            }

            // World moved the lease: this instance must neither save nor release the character any more.
            _world.TransferredAway(characterId);
            try
            {
                if (IPAddress.TryParse(reply.Target.Address, out IPAddress? address) && reply.Target.Port is > 0 and <= ushort.MaxValue
                    && ReferenceEquals(player.Session, session))
                {
                    // A proxy instance hosted elsewhere: the teleport still names the interior the client loads.
                    // The client is told a door's proxy instance as the interior it copies (Playfield.ClientPlayfieldId).
                    int clientPlayfieldId = destinationPlayfield;
                    if (ZoneEngine_New.Core.Playfield.ProxyPlayfieldIds.IsProxy(destinationPlayfield)
                        && !ZoneEngine_New.Core.Playfield.ProxyPlayfieldIds.TryResolveResource(_gameData, destinationPlayfield, out clientPlayfieldId, out _, out _))
                        clientPlayfieldId = destinationPlayfield;

                    foreach (byte[] packet in session.PrepareRemoteTransferPackets(player, landing, heading, clientPlayfieldId,
                        address, (ushort)reply.Target.Port))
                        session.Send(packet);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Transfer redirect failed for character " + characterId.ToString(CultureInfo.InvariantCulture));
            }

            if (ReferenceEquals(player.Playfield, source))
            {
                if (clearReturnDoor)
                    source.WorldAccess.Instance?.ForgetCharacterTriggers(characterId);
                source.GetRequiredService<SpawnService>().DepartToInstance(player);
            }

            // The client reconnects to the destination by itself; drop this socket once it has had time to.
            _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => session.Close(), TaskScheduler.Default);
            _logger.Info(string.Format(CultureInfo.InvariantCulture,
                "Cross-instance transfer character={0} handed to {1} ({2}:{3})", characterId, reply.TargetInstance,
                reply.Target.Address, reply.Target.Port));
        }

        void Restore(Player player, ZoneSession session, string message)
        {
            try
            {
                if (!player.IsPersistenceQuarantined && _world.OwnsLease(player.Identity.Instance))
                    _snapshot.RestoreOnline(player.Identity.Instance);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Restoring Online failed for character " + player.Identity.Instance.ToString(CultureInfo.InvariantCulture));
            }

            session.AbortRemoteTransfer();
            Tell(player, message);
        }

        internal static TransferEnvelope BuildEnvelope(Player player, int destinationPlayfield, Vector3 landing, Quaternion heading,
            ArrivalKind arrival, int exitProxyDoor, bool clearReturnDoor)
        {
            var envelope = new TransferEnvelope
            {
                DestinationPlayfield = destinationPlayfield,
                X = landing.xf,
                Y = landing.yf,
                Z = landing.zf,
                HeadingX = heading.xf,
                HeadingY = heading.yf,
                HeadingZ = heading.zf,
                HeadingW = heading.wf,
                Arrival = arrival,
                ExitProxyDoor = exitProxyDoor,
                ClearReturnDoor = clearReturnDoor,
                SavedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            DateTime now = DateTime.UtcNow;
            foreach (NpcCharacter pet in player.OwnedPets.All.ToList())
            {
                // A charm holds only where it was cast.
                // A tower stays behind and waits (PetService.HoldStayBehindPets).
                if (pet.Pet is not { } controller || controller.IsCharmed || pet.IsDead || controller.HasExpired(now)
                    || Pets.PetTypes.StaysBehind(controller.Type))
                    continue;
                int max = pet.Stats.GetOrZero(CharacterStat.MaxHealth);
                envelope.Pets.Add(new PetEnvelope
                {
                    Hash = controller.Hash,
                    Level = controller.Level,
                    HealthFraction = max > 0 ? Math.Clamp(pet.Stats.GetOrZero(CharacterStat.Health) / (float)max, 0.01f, 1f) : 1f,
                    RemainingSeconds = controller.ExpiresUtc is DateTime expires
                        ? Math.Max(1, (int)Math.Ceiling((expires - now).TotalSeconds))
                        : 0,
                    Mode = (int)controller.Mode,
                    AggDef = pet.Stats.GetOrZero(CharacterStat.AggDef, StatDetail.Base)
                });
            }

            return envelope;
        }

        static void Tell(Player player, string text)
            => player.Session?.Send(new SmokeLounge.AOtomation.Messaging.Messages.N3Messages.ChatTextMessage
            {
                Identity = player.Identity,
                Text = text,
                Unknown1 = 0,
                Unknown2 = 0,
                Unknown3 = 0
            });
    }

    /// <summary>The arriving side of a cross-instance move.</summary>
    public static class EnvelopeApplier
    {
        /// <summary>Summons again the pets the character brought, beside it, with their health and remaining time.</summary>
        public static void SummonPets(PlayfieldInstance playfield, Player player, TransferEnvelope envelope, IZoneLogger logger)
        {
            if (envelope.Pets.Count == 0) return;
            var pets = playfield.GetRequiredService<Pets.PetService>();
            foreach (PetEnvelope carried in envelope.Pets)
            {
                try
                {
                    NpcCharacter? pet = pets.Summon(player, carried.Hash, carried.Level, carried.RemainingSeconds);
                    if (pet == null) continue;
                    int max = pet.Stats.GetOrZero(CharacterStat.MaxHealth);
                    if (max > 0)
                        pet.Stats.Set(CharacterStat.Health, Math.Max(1, (int)Math.Round(max * carried.HealthFraction)), StatDetail.Base, dirty: true);
                    if (carried.HasAggDef)
                        pet.Stats.Set(CharacterStat.AggDef, carried.AggDef, StatDetail.Base, dirty: true);
                    if (Enum.IsDefined(typeof(Pets.PetMode), carried.Mode))
                        pet.Pet?.Order((Pets.PetMode)carried.Mode);
                }
                catch (Exception exception)
                {
                    logger.Error(exception, "Re-summoning pet " + carried.Hash + " for character "
                        + player.Identity.Instance.ToString(CultureInfo.InvariantCulture) + " failed");
                }
            }
        }
    }
}
