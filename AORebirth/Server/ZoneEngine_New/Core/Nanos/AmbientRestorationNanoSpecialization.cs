namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Teams;

    /// <summary>
    /// Accepted 20260722-keeper-exect-nano contract: immediate first pulse, then one
    /// level-selected child every 20 seconds. Not generic TeamCast of all four children.
    /// </summary>
    public sealed class AmbientRestorationNanoSpecialization : INanoSpecialization
    {
        public const int ParentNanoId = 302365;
        readonly TeamService _teams;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly object _gate = new();
        readonly Dictionary<Player, (IZoneSession Session, DateTimeOffset Next)> _auras = new();
        internal TimeProvider Clock { get; set; } = TimeProvider.System;

        public AmbientRestorationNanoSpecialization(TeamService teams, Lazy<PlayfieldManager> playfields)
        {
            _teams = teams ?? throw new ArgumentNullException(nameof(teams));
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
        }

        public bool Handles(int nanoId) => nanoId == ParentNanoId;

        public bool TryPrepare(Player caster, Player target, NanoDefinition nano, out NanoSpecializationPlan plan)
        {
            // Legacy's specialized branch does not call SetNanoDuration. Adding a parent
            // NCU entry here would be a new contract, not migration of the accepted aura.
            plan = new NanoSpecializationPlan(false, 0);
            return Handles(nano.Id) && ReferenceEquals(caster, target) && IsLive(caster);
        }

        public void Applied(Player caster, Player target, NanoDefinition nano, ActiveNanoRecord? active)
        {
            if (!Handles(nano.Id) || !ReferenceEquals(caster, target) || !IsLive(caster)) return;
            lock (_gate) _auras[caster] = (caster.Session!, Clock.GetUtcNow().AddSeconds(20));
            Pulse(caster);
        }

        public void Tick(Player caster)
        {
            lock (_gate)
            {
                if (!_auras.TryGetValue(caster, out var aura)) return;
                if (!IsLive(caster) || !ReferenceEquals(caster.Session, aura.Session))
                {
                    _auras.Remove(caster);
                    return;
                }
                DateTimeOffset now = Clock.GetUtcNow();
                if (now < aura.Next) return;
                // Match Legacy's non-burst schedule after a delayed owner tick.
                _auras[caster] = (aura.Session, now.AddSeconds(20));
            }
            Pulse(caster);
        }

        public void Removed(Player target, int nanoId) { if (Handles(nanoId)) Detached(target); }
        public void Detached(Player player) { lock (_gate) _auras.Remove(player); }
        public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active)
        {
            // There is no supported durable aura row to restore. Never synthesize a cast.
        }

        static bool IsLive(Player player) => player.Session?.State == SessionState.InPlay
            && player.Playfield != null && !player.IsDead && !player.IsPersistenceQuarantined
            && player.Stats.GetOrZero(CharacterStat.Health) > 0;

        void Pulse(Player caster)
        {
            if (!IsLive(caster)) return;
            (int child, int amount) = ResolveTier(caster.Stats.GetOrZero(CharacterStat.Level));
            PlayfieldLocality locality = caster.Playfield!.GetRequiredService<PlayfieldLocality>();
            locality.Announce(caster, TriggeredCast(caster, ParentNanoId), includeSelf: true);
            locality.Announce(caster, TriggeredCast(caster, child), includeSelf: true);
            var recipients = new HashSet<Player> { caster };
            TeamSnapshot? team = _teams.GetTeam(caster);
            if (team != null)
            {
                foreach (int id in team.MemberIds)
                {
                    if (_playfields.Value.FindPlayer(id, out Player member) && IsLive(member)
                        && ReferenceEquals(member.Playfield, caster.Playfield)) recipients.Add(member);
                }
            }
            foreach (Player recipient in recipients)
            {
                lock (recipient.PersistenceGate)
                {
                    if (!IsLive(recipient) || !ReferenceEquals(recipient.Playfield, caster.Playfield)) continue;
                    int health = recipient.Stats.GetOrZero(CharacterStat.Health);
                    long room = Math.Max(0L, (long)Math.Max(1, recipient.Stats.GetOrZero(CharacterStat.MaxHealth)) - health);
                    int restored = (int)Math.Min(amount, room);
                    if (restored > 0)
                    {
                        recipient.Stats.Set(CharacterStat.Health, health + restored, StatDetail.Base, dirty: true);
                        recipient.FlushDirtyStats();
                    }
                    // Captured sparkle remains visible at full health too.
                    locality.Announce(recipient, BuildVisual(recipient), includeSelf: true);
                }
            }
        }

        internal static (int Child, int Amount) ResolveTier(int level) => level >= 150 ? (300498, 243)
            : level >= 100 ? (300497, 143) : level >= 50 ? (300496, 53) : (300495, 10);

        static CastNanoSpellMessage TriggeredCast(Player caster, int nanoId) => new()
        {
            Identity = caster.Identity, Caster = caster.Identity, Target = caster.Identity,
            NanoId = nanoId, Unknown = 0, Unknown1 = 1
        };

        internal static SpellListMessage BuildVisual(Player recipient) => new()
        {
            Identity = recipient.Identity, Unknown = 0, Character = recipient.Identity, NanoName = "Ambient Restoration",
            NanoEffects = [new NanoEffect
            {
                Effect = new Identity { Type = (IdentityType)0xCF4A, Instance = ParentNanoId },
                Unknown1 = 4, CriterionCount = 1, Hits = 0x80, Delay = 0x90,
                Unknown2 = 1, Unknown3 = 1, GfxValue = 0, GfxLife = 2, GfxSize = 9,
                GfxRed = 300495, GfxGreen = (int)IdentityType.CanbeAffected, GfxBlue = recipient.Identity.Instance, GfxFade = 0
            }]
        };
    }
}
