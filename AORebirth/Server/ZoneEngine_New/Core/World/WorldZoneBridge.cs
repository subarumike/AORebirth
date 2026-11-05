namespace ZoneEngine_New.Core.World
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using AORebirth.World.Contracts.V1;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;

    using PlayfieldInstance = ZoneEngine_New.Core.Playfield.Playfield;

    /// <summary>Answers World's requests against this instance's playfields. Each player is touched on its own tick.</summary>
    public sealed class WorldZoneBridge : IWorldZoneHandlers
    {
        readonly Lazy<PlayfieldManager> _manager;
        readonly Lazy<ZoneEngine_New.Core.Teams.TeamService>? _teams;
        readonly ICharacterRepository _characters;
        readonly IZoneLogger _logger;

        /// <summary>Lazy: World connects before the playfield manager (and the modules it loads) exists.</summary>
        public WorldZoneBridge(Lazy<PlayfieldManager> playfields, ICharacterRepository characters, IZoneLogger logger,
            Lazy<ZoneEngine_New.Core.Teams.TeamService>? teams = null)
        {
            _teams = teams;
            _manager = playfields ?? throw new ArgumentNullException(nameof(playfields));
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        PlayfieldManager _playfields => _manager.Value;

        public IReadOnlyList<PlayfieldLoad> Load()
        {
            if (!_manager.IsValueCreated) return Array.Empty<PlayfieldLoad>();
            var counts = new Dictionary<int, int>();
            foreach (PlayfieldInstance playfield in _playfields.SnapshotPlayfields())
                counts[playfield.Identity.Instance] = 0;
            foreach (Player player in _playfields.SnapshotPlayers())
            {
                int id = player.Playfield?.Identity.Instance ?? 0;
                if (id > 0) counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
            }

            return counts.Select(pair => new PlayfieldLoad { PlayfieldId = pair.Key, Players = pair.Value }).ToList();
        }

        public IReadOnlyList<PlayerPresence> Presence()
            => !_manager.IsValueCreated ? Array.Empty<PlayerPresence>() : _playfields.SnapshotPlayers().Select(player => new PlayerPresence(
                player.Identity.Instance,
                player.Name ?? string.Empty,
                player.Playfield?.Identity.Instance ?? 0,
                player.ConnectionPhase == PlayerConnectionPhase.LinkDead,
                player.Stats.GetOrZero(CharacterStat.Level),
                player.Stats.GetOrZero(CharacterStat.Profession),
                Card(player))).ToList();

        /// <summary>
        /// What other instances show of this player (inspect, team rows). Current health is left out: it changes all
        /// the time and only matters where the player is.
        /// </summary>
        static PlayerCard Card(Player player)
        {
            bool hasVitals = player.Stats.TryGetValue(CharacterStat.MaxHealth, out int health) && health > 0;
            hasVitals &= player.Stats.TryGetValue(CharacterStat.MaxNanoEnergy, out int nano)
                && nano >= 0 && !StatCollection.IsUnset(nano);
            double sideXp = player.Playfield?.GetService<ZoneEngine_New.Core.Configuration.GameplaySettings>()?.Xp.SideBonusPercent ?? 0;
            return new PlayerCard
            {
                CharacterId = player.Identity.Instance,
                Name = player.Name ?? player.FirstName ?? string.Empty,
                FirstName = player.FirstName ?? string.Empty,
                LastName = player.LastName ?? string.Empty,
                Level = player.Stats.GetOrOne(CharacterStat.Level),
                TitleLevel = player.Stats.GetOrOne(CharacterStat.TitleLevel),
                Profession = player.Stats.GetOrZero(CharacterStat.Profession),
                VisualProfession = player.Stats.GetOrZero(CharacterStat.VisualProfession),
                SideXp = (int)Math.Round(sideXp * 100, MidpointRounding.AwayFromZero),
                MaxHealth = Math.Max(0, health),
                MaxNano = hasVitals ? nano : 0,
                HasVitals = hasVitals,
                InvadersKilled = player.Stats.GetOrZero(CharacterStat.InvadersKilled),
                KilledByInvaders = player.Stats.GetOrZero(CharacterStat.KilledByInvaders),
                AiLevel = player.Stats.GetOrZero(CharacterStat.AlienLevel),
                Side = player.Stats.GetOrZero(CharacterStat.Side)
            };
        }

        public Task<ReleaseOutcome> ReleaseCharacterAsync(int characterId)
        {
            if (!_playfields.FindPlayer(characterId, out Player player) || player.Playfield is not PlayfieldInstance playfield)
                return Task.FromResult(ReleaseOutcome.ReleaseNotHere);
            if (player.ConnectionPhase != PlayerConnectionPhase.LinkDead && player.Session != null)
                return Task.FromResult(ReleaseOutcome.ReleaseInPlay);

            var done = new TaskCompletionSource<ReleaseOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            playfield.DispatchPlayerProjection(player, () =>
            {
                try
                {
                    // Re-check on the tick: a client may have reconnected meanwhile.
                    if (!ReferenceEquals(player.Playfield, playfield))
                    {
                        done.TrySetResult(ReleaseOutcome.ReleaseFailed);
                        return;
                    }

                    if (player.ConnectionPhase != PlayerConnectionPhase.LinkDead && player.Session != null)
                    {
                        done.TrySetResult(ReleaseOutcome.ReleaseInPlay);
                        return;
                    }

                    playfield.GetRequiredService<SpawnService>().LogoutPlayer(player);
                    _logger.Info("Released link-dead character " + characterId.ToString(CultureInfo.InvariantCulture) + " for deletion");
                    done.TrySetResult(ReleaseOutcome.ReleaseDone);
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Release of character " + characterId.ToString(CultureInfo.InvariantCulture) + " failed");
                    done.TrySetResult(ReleaseOutcome.ReleaseFailed);
                }
            });
            return done.Task;
        }

        public LocateCharacterReply Locate(int characterId)
        {
            if (!_playfields.FindPlayer(characterId, out Player player) || player.Playfield is not PlayfieldInstance playfield)
                return new LocateCharacterReply { Found = false };
            AORebirth.Core.Vector.Vector3 position = player.Position;
            return new LocateCharacterReply
            {
                Found = true,
                PlayfieldId = playfield.Identity.Instance,
                X = position.xf,
                Y = position.yf,
                Z = position.zf
            };
        }

        public void ClearOnline(IReadOnlyList<int> characterIds)
        {
            foreach (int id in characterIds)
            {
                if (_playfields.FindPlayer(id, out _)) continue;
                try { _characters.SetOffline(id); }
                catch (Exception exception) { _logger.Warn("Clearing Online for character " + id + " failed: " + exception.GetType().Name); }
            }
        }

        public void Revoke(int characterId, string reason)
        {
            if (!_playfields.FindPlayer(characterId, out Player player) || player.Playfield is not PlayfieldInstance playfield)
                return;
            // Never write a character another owner has: quarantine first, then let it go.
            player.QuarantinePersistence();
            playfield.DispatchPlayerProjection(player, () =>
            {
                if (ReferenceEquals(player.Playfield, playfield))
                    playfield.GetRequiredService<SpawnService>().LogoutPlayer(player);
            });
            _logger.Error("World revoked character " + characterId.ToString(CultureInfo.InvariantCulture) + " (" + reason + ")");
        }

        public void LogoutAll(string reason)
        {
            foreach (Player player in _playfields.SnapshotPlayers())
            {
                if (player.Playfield is not PlayfieldInstance playfield) continue;
                Send(player, "You are being logged out: " + reason + ".");
                playfield.DispatchPlayerProjection(player, () =>
                {
                    if (ReferenceEquals(player.Playfield, playfield))
                        playfield.GetRequiredService<SpawnService>().LogoutPlayer(player);
                });
            }
        }

        public void GameClockChanged(long offsetGameSeconds)
        {
            GameClock.SetOffset(offsetGameSeconds);
            foreach (Player player in _playfields.SnapshotPlayers())
            {
                if (player.Playfield is not PlayfieldInstance playfield || player.Session == null) continue;
                playfield.DispatchPlayerProjection(player, () =>
                {
                    if (player.Session != null && player.Playfield is PlayfieldInstance current)
                        current.GetRequiredService<SpawnService>().SendGameTime(player.Session, player);
                });
            }
        }

        public IReadOnlyList<TeamState> Teams()
            => _teams is { IsValueCreated: true } ? _teams.Value.SnapshotMirrors() : Array.Empty<TeamState>();

        public void DutyChanged(bool holder)
        {
        }

        public void Announce(string text)
        {
            foreach (Player player in _playfields.SnapshotPlayers()) Send(player, text);
        }

        static void Send(Player player, string text)
        {
            if (player.Session == null || player.Playfield is not PlayfieldInstance playfield) return;
            playfield.DispatchPlayerProjection(player, () =>
                player.Session?.Send(new ChatTextMessage
                {
                    Identity = player.Identity,
                    Text = text,
                    Unknown1 = 0,
                    Unknown2 = 0,
                    Unknown3 = 0
                }));
        }
    }
}
