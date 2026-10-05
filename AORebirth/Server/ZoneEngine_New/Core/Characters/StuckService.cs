namespace ZoneEngine_New.Core.Characters
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Playfield;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    /// <summary>
    /// /stuck: sends the player to <see cref="GameplaySettings.StuckLocation"/>. Refused while something is attacking the
    /// player or while the Stability skill is still locked from the last /stuck; a successful /stuck locks Stability for
    /// <see cref="GameplaySettings.StuckCooldownSeconds"/>.
    /// </summary>
    public sealed class StuckService
    {
        readonly GameplaySettings _settings;
        readonly Lazy<PlayfieldManager> _playfields;

        public StuckService(GameplaySettings settings, Lazy<PlayfieldManager> playfields)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(playfields);
            _settings = settings;
            _playfields = playfields;
        }

        public void TryUnstick(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);

            if (player.Playfield is not Playfield current || player.Session == null)
                return;

            if (player.IsBeingFought)
            {
                RequirementFeedback.SendText(player, "You can't use /stuck while you are being attacked.");
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            TimeSpan wait = player.SkillLocks.Remaining((int)CharacterStat.Stability, nowUtc);
            if (wait > TimeSpan.Zero)
            {
                // The same "skill is locked, able in hh:mm:ss" line as a locked special attack or item.
                player.SendSkillLocked((int)CharacterStat.Stability, wait);
                return;
            }

            GameplayLocation target = _settings.StuckLocation!;
            var landing = new Vector3(target.X, target.Y, target.Z);
            if (target.Playfield == current.Identity.Instance)
            {
                LockStability(player, nowUtc);
                player.TeleportWithinPlayfield(landing);
                return;
            }

            // Not a proxy entry, so it leaves no way back through an exit proxy (as a GM jump does).
            player.Stats.Set(CharacterStat.ExternalPlayfieldInstance, 0, StatDetail.Base, dirty: true);
            player.Stats.Set(CharacterStat.ExternalDoorInstance, 0, StatDetail.Base, dirty: true);
            bool queued = _playfields.Value.WithPlayfield(target.Playfield, player, destination =>
            {
                if (ReferenceEquals(player.Playfield, current))
                    player.Session?.TransferToPlayfield(destination, landing);
            });

            if (queued)
                LockStability(player, nowUtc);
        }

        /// <summary>The configured cooldown as-is: a Stability lock, not scaled by SkillLockModifier.</summary>
        void LockStability(Player player, DateTime nowUtc)
        {
            int seconds = _settings.StuckCooldownSeconds;
            if (seconds <= 0)
                return;

            player.SkillLocks.Lock((int)CharacterStat.Stability, seconds, nowUtc);
            player.Playfield?.GetService<InventoryFlushService>()?.NotifyDirty(player);
            player.ShowSkillLock((int)CharacterStat.Stability, seconds);
        }
    }
}
