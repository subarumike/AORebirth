namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Linq;
    using ZoneEngine_New.Core.GameData;
    using System.Collections.Generic;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Teams;

    /// <summary>Periodic team healing with editable nano bindings, tiers, timing and visuals.</summary>
    public sealed class PeriodicTeamHealNanoSpecialization : INanoSpecialization
    {
        private readonly NanoMechanicCatalog _mechanics;
        readonly TeamService _teams;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly object _gate = new();
        readonly Dictionary<Player, (IZoneSession Session, DateTimeOffset Next, NanoMechanicDefinition Definition)> _auras = new();
        internal TimeProvider Clock { get; set; } = TimeProvider.System;

        public PeriodicTeamHealNanoSpecialization(TeamService teams, Lazy<PlayfieldManager> playfields, NanoMechanicCatalog? mechanics = null)
        {
            _mechanics = mechanics ?? NanoMechanicCatalog.LoadDefault();
            _teams = teams ?? throw new ArgumentNullException(nameof(teams));
            _playfields = playfields ?? throw new ArgumentNullException(nameof(playfields));
        }

        public bool Handles(int nanoId) => _mechanics.TryGet(nanoId, NanoMechanicKind.PeriodicTeamHeal, out _);

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
            var definition = _mechanics.Get(nano.Id, NanoMechanicKind.PeriodicTeamHeal);
            lock (_gate) _auras[caster] = (caster.Session!, NextPulse(Clock.GetUtcNow(), definition.PulseSeconds), definition);
            Pulse(caster, definition);
        }

        public void Tick(Player caster)
        {
            NanoMechanicDefinition definition;
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
                definition = aura.Definition;
                _auras[caster] = (aura.Session, NextPulse(now, definition.PulseSeconds), definition);
            }
            Pulse(caster, definition);
        }

        public void Removed(Player target, int nanoId) { if (Handles(nanoId)) Detached(target); }
        private static DateTimeOffset NextPulse(DateTimeOffset now, double seconds)
        {
            // Editable intervals must not overflow wall-clock scheduling after a committed cast.
            if (seconds >= (DateTimeOffset.MaxValue - now).TotalSeconds) return DateTimeOffset.MaxValue;
            try { return now.AddSeconds(seconds); }
            catch (ArgumentOutOfRangeException) { return DateTimeOffset.MaxValue; }
        }
        public void Detached(Player player) { lock (_gate) _auras.Remove(player); }
        public void Restored(Player target, NanoDefinition nano, ActiveNanoRecord active)
        {
            // There is no supported durable aura row to restore. Never synthesize a cast.
        }

        static bool IsLive(Player player) => player.Session?.State == SessionState.InPlay
            && player.Playfield != null && !player.IsDead && !player.IsPersistenceQuarantined
            && player.Stats.GetOrZero(CharacterStat.Health) > 0;

        void Pulse(Player caster, NanoMechanicDefinition definition)
        {
            if (!IsLive(caster)) return;
            (int child, int amount) = ResolveTier(caster.Stats.GetOrZero(CharacterStat.Level), definition);
            PlayfieldLocality locality = caster.Playfield!.GetRequiredService<PlayfieldLocality>();
            locality.Announce(caster, TriggeredCast(caster, definition.NanoId), includeSelf: true);
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
                    if (definition.PulseVisual is { } visual) locality.Announce(recipient, BuildVisual(recipient, visual), includeSelf: true);
                }
            }
        }

        internal static (int Child, int Amount) ResolveTier(int level, NanoMechanicDefinition definition)
        {
            var tier = definition.HealTiers.Where(t => level >= t.MinimumLevel).OrderByDescending(t => t.MinimumLevel).First();
            return (tier.NanoId, tier.Amount);
        }

        static CastNanoSpellMessage TriggeredCast(Player caster, int nanoId) => new()
        {
            Identity = caster.Identity, Caster = caster.Identity, Target = caster.Identity,
            NanoId = nanoId, Unknown = 0, Unknown1 = 1
        };

        internal static SpellListMessage BuildVisual(Player recipient, NanoVisualEffect visual) => new()
        {
            Identity = recipient.Identity, Unknown = 0, Character = recipient.Identity, NanoName = visual.Name,
            NanoEffects = [new NanoEffect
            {
                Effect = new Identity { Type = (IdentityType)visual.EffectType, Instance = visual.EffectId },
                Unknown1 = 4, CriterionCount = 1, Hits = visual.Hits, Delay = visual.Delay,
                Unknown2 = 1, Unknown3 = 1, GfxValue = 0, GfxLife = visual.Life, GfxSize = visual.Size,
                GfxRed = visual.GraphicId, GfxGreen = (int)IdentityType.CanbeAffected, GfxBlue = recipient.Identity.Instance, GfxFade = 0
            }]
        };
    }
}
