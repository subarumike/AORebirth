namespace ZoneEngine_New.Core.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using AORebirth.Modules.World;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;

    /// <summary>
    /// The module API's actions. Each is queued onto the playfield tick that owns its target
    /// (<see cref="Playfield.Dispatch"/>), never run under a lock taken from the module's thread. The named operations
    /// (module API 1.5) are in ZoneModuleActions.Operations.cs.
    /// </summary>
    internal sealed partial class ZoneModuleActions : IZoneActions
    {
        /// <summary>A task whose playfield went away before running it completes false after this.</summary>
        internal static TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

        readonly Lazy<PlayfieldManager> _manager;
        readonly Func<ZoneEngine_New.Core.Accounts.AccountSanctionService>? _accounts;
        readonly string? _gameDataRoot;
        readonly ZoneEngine_New.Core.Logging.IZoneLogger? _logger;
        readonly Func<ZoneEngine_New.Core.Lifecycle.ShutdownScheduler>? _shutdown;
        readonly Dictionary<string, Operation> _operations;

        /// <param name="accounts">Account suspensions and bans, for the account operations; null leaves them unregistered.</param>
        /// <param name="gameDataRoot">The GameData tree, to check a teleport's playfield exists; null skips the check.</param>
        /// <param name="shutdown">The engine's timed shutdown, for the server operations; null leaves them unregistered.</param>
        public ZoneModuleActions(Func<PlayfieldManager> manager,
            Func<ZoneEngine_New.Core.Accounts.AccountSanctionService>? accounts = null, string? gameDataRoot = null,
            ZoneEngine_New.Core.Logging.IZoneLogger? logger = null,
            Func<ZoneEngine_New.Core.Lifecycle.ShutdownScheduler>? shutdown = null)
        {
            ArgumentNullException.ThrowIfNull(manager);
            _manager = new Lazy<PlayfieldManager>(manager);
            _accounts = accounts;
            _gameDataRoot = gameDataRoot;
            _logger = logger;
            _shutdown = shutdown;
            _operations = BuildOperations();
        }

        public Task<bool> RefreshNpc(int playfieldId, int instance)
        {
            Playfield? playfield = null;
            foreach (Playfield candidate in _manager.Value.SnapshotPlayfields())
            {
                if (candidate.Identity.Instance == playfieldId)
                {
                    playfield = candidate;
                    break;
                }
            }

            if (playfield == null)
                return Task.FromResult(false);

            Playfield owner = playfield;
            return Run(owner, () => RefreshOnTick(owner, instance));
        }

        public Task<bool> SendSystemMessage(int characterId, string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return OnPlayer(characterId, player =>
            {
                if (player.Session is not { } session)
                    return false;
                session.Send(new ChatTextMessage
                {
                    Identity = player.Identity, Text = text, Unknown1 = 0, Unknown2 = 0, Unknown3 = 0
                });
                return true;
            });
        }

        public Task<bool> SendFeedback(int characterId, int feedbackId)
            => OnPlayer(characterId, player =>
            {
                if (player.Session == null)
                    return false;
                ClientFeedback.Send(player, feedbackId);
                return true;
            });

        /// <summary>
        /// Sends <paramref name="instance"/>'s spawn extras again to its observers, with an explicit HasKnubotData 0 when it
        /// no longer talks through a script. Runs on the owning tick.
        /// </summary>
        public Task<bool> RespawnNpc(int playfieldId, int instance)
        {
            Playfield? playfield = null;
            foreach (Playfield candidate in _manager.Value.SnapshotPlayfields())
            {
                if (candidate.Identity.Instance == playfieldId)
                {
                    playfield = candidate;
                    break;
                }
            }

            if (playfield == null)
                return Task.FromResult(false);

            Playfield owner = playfield;
            return Run(owner, () => RespawnOnTick(owner, instance));
        }

        /// <summary>The NPC's spawn point, despawned as dead and spawned again now (<see cref="HashSpawnSystem.TryRespawnNow"/>).</summary>
        internal static bool RespawnOnTick(Playfield playfield, int instance)
        {
            playfield.RequireTick();
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(identity, out Dynel? dynel)
                || dynel is not NpcCharacter npc || !ReferenceEquals(npc.Playfield, playfield))
                return false;

            return playfield.GetService<HashSpawnSystem>()?.TryRespawnNow(npc) ?? false;
        }

        internal static bool RefreshOnTick(Playfield playfield, int instance)
        {
            playfield.RequireTick();
            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = instance };
            if (!playfield.GetRequiredService<DynelRegistry>().TryGet(identity, out Dynel? dynel)
                || dynel is not NpcCharacter npc
                || !ReferenceEquals(npc.Playfield, playfield))
                return false;

            Knubot.KnubotNpcRefresh.Refresh(playfield, npc);
            return true;
        }

        Task<bool> OnPlayer(int characterId, Func<Player, bool> action) => OnPlayer(characterId, action, false);

        /// <summary>Runs <paramref name="action"/> on the player's tick; <paramref name="notHere"/> when it is not in play here.</summary>
        Task<T> OnPlayer<T>(int characterId, Func<Player, T> action, T notHere)
        {
            if (!_manager.Value.FindPlayer(characterId, out Player player) || player.Playfield is not Playfield playfield)
                return Task.FromResult(notHere);

            // The player can change playfield before the queued work runs: it then runs on (or is passed to) the new one.
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            playfield.DispatchPlayerProjection(player, () =>
            {
                try
                {
                    done.TrySetResult(_manager.Value.FindPlayer(characterId, out Player current)
                        && ReferenceEquals(current, player) ? action(player) : notHere);
                }
                catch (Exception exception)
                {
                    done.TrySetException(exception);
                }
            });
            return Bounded(done.Task, notHere);
        }

        static Task<bool> Run(Playfield playfield, Func<bool> action)
        {
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool queued = playfield.Dispatch(() =>
            {
                try
                {
                    done.TrySetResult(action());
                }
                catch (Exception exception)
                {
                    done.TrySetException(exception);
                }
            });
            return queued ? Bounded(done.Task, false) : Task.FromResult(false);
        }

        /// <summary>Work queued on a playfield that is retired before its next tick never runs: report <paramref name="timedOut"/>.</summary>
        static async Task<T> Bounded<T>(Task<T> task, T timedOut)
        {
            try
            {
                return await task.WaitAsync(Timeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return timedOut;
            }
        }
    }
}
