namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>Why an active buff left NCU. Only <see cref="Cancelled"/> comes from the owner.</summary>
    public enum BuffRemovalReason
    {
        Expired,
        Cancelled,
        Death,

        /// <summary>A spell removed the buff. Ignores <see cref="ItemTemplate.CanCancel"/>.</summary>
        Stripped,
    }

    /// <summary>Result of asking a character to drop one buff.</summary>
    public enum BuffRemovalOutcome
    {
        Removed,
        NotFound,
        NotCancellable,
    }

    /// <summary>
    /// One applied instance of a <see cref="NanoSpell"/>. Expiry is a UTC deadline, never a
    /// countdown, so a stalled or skipped tick cannot extend a buff.
    /// </summary>
    public sealed class Buff : NanoSpell
    {
        /// <summary>Legacy duration ceiling (100 hours in centiseconds).</summary>
        public const int MaxDurationCentiseconds = 36000000;

        readonly List<PeriodicEffect> _periodic = new();

        Buff(NanoSpell spell, Identity source, int nanoInstance, DateTime expiresAtUtc)
            : base(spell)
        {
            Source = source;
            NanoInstance = nanoInstance;
            ExpiresAtUtc = expiresAtUtc;
        }

        /// <summary>Hits and nano casts: the OnUse functions a buff can repeat or delay from its NCU entry.</summary>
        internal static bool IsTimedFunction(ItemSpell spell)
            => (FunctionType)spell.FunctionType is FunctionType.Hit or FunctionType.CastNano
                or FunctionType.TeamCastNano or FunctionType.AreaCastNano or FunctionType.PlayfieldNano
                or FunctionType.CastNanoIfPossible or FunctionType.NpcCastNanoIfPossible
                or FunctionType.CastNanoIfPossibleOnFightTarget or FunctionType.NpcCastNanoIfPossibleOnFightTarget;

        /// <summary>
        /// One tick after a delay (TickCount 1, TickInterval N): runs once, N centiseconds after landing, and not at the
        /// land. Proc buffs refresh this way: Soothing Herbs 263341 lasts 50 s and recasts itself at 45 s.
        /// </summary>
        internal static bool IsDelayedOnce(ItemSpell spell)
            => spell.TickCount == 1 && spell.TickInterval > 0 && IsTimedFunction(spell);

        /// <summary>
        /// Repeated hits and child-nano casts use their data-defined count and interval (centiseconds).
        /// Landing runs the first application; the rest run only while the parent buff remains active.
        /// A delayed single tick (<see cref="IsDelayedOnce"/>) is not run by the land; it falls due one interval
        /// after <paramref name="landedUtc"/>.
        /// </summary>
        void SchedulePeriodic(DateTime nowUtc, DateTime landedUtc, int hitsAlreadyDone)
        {
            _periodic.Clear();
            if (!SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return;

            for (int i = 0; i < spells.Count; i++)
            {
                ItemSpell spell = spells[i];
                if (IsDelayedOnce(spell))
                {
                    _periodic.Add(new PeriodicEffect(spell, 1, landedUtc.AddMilliseconds(spell.TickInterval * 10.0)));
                    continue;
                }

                if (!IsTimedFunction(spell) || spell.TickCount <= 1 || spell.TickInterval == 0)
                    continue;

                int remaining = spell.TickCount - hitsAlreadyDone;
                if (remaining > 0)
                    _periodic.Add(new PeriodicEffect(spell, remaining, nowUtc.AddMilliseconds(spell.TickInterval * 10.0)));
            }
        }

        /// <summary>
        /// Periodic functions due by <paramref name="nowUtc"/>. A tick that would land at or after expiry
        /// is dropped. Each returned entry is one tick.
        /// </summary>
        public void CollectDueTicks(DateTime nowUtc, List<ItemSpell> due)
        {
            ArgumentNullException.ThrowIfNull(due);
            for (int i = 0; i < _periodic.Count; i++)
            {
                PeriodicEffect effect = _periodic[i];
                while (effect.Remaining > 0 && effect.NextUtc <= nowUtc && effect.NextUtc < ExpiresAtUtc)
                {
                    due.Add(effect.Spell);
                    effect.Remaining--;
                    effect.NextUtc = effect.NextUtc.AddMilliseconds(effect.Spell.TickInterval * 10.0);
                }
            }
        }

        sealed class PeriodicEffect(ItemSpell spell, int remaining, DateTime nextUtc)
        {
            public ItemSpell Spell { get; } = spell;

            public int Remaining { get; set; } = remaining;

            public DateTime NextUtc { get; set; } = nextUtc;
        }

        /// <summary>Caster identity. Kept so buffs can be attributed and audited.</summary>
        public Identity Source { get; }

        /// <summary>Per-character NCU entry id the client uses to address this buff.</summary>
        public int NanoInstance { get; }

        public DateTime ExpiresAtUtc { get; private set; }

        /// <summary>
        /// Brings the end forward by <paramref name="amount"/> (ReduceNanoStrainDuration). The caller removes a buff
        /// this runs out; periodic ticks already stop at the expiry (<see cref="CollectDueTicks"/>).
        /// </summary>
        internal void Shorten(TimeSpan amount)
        {
            if (amount > TimeSpan.Zero)
                ExpiresAtUtc -= amount;
        }

        /// <summary>
        /// Buff functions the target runs while the nano is up. Reapplied by every rebase,
        /// so a removed buff needs no inverse operation.
        /// </summary>
        public IReadOnlyList<ItemSpell> ModifierSpells
        {
            get
            {
                if (SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? use))
                    return use;
                return SpellList.TryGetValue(EventType.OnWear, out List<ItemSpell>? wear) ? wear : [];
            }
        }

        /// <summary>
        /// The absorb layers this buff grants: ChangeVariable on an Absorb AC stat (238-246; 273386 gives 1334 to each of
        /// Projectile through Poison). The stat is the pool of damage of that type still absorbed. The owner adds the grant
        /// when the buff lands and takes back what is left of it when the buff leaves.
        /// </summary>
        public IEnumerable<(CharacterStat Stat, int Amount)> AbsorbGrants
        {
            get
            {
                IReadOnlyList<ItemSpell> spells = ModifierSpells;
                for (int i = 0; i < spells.Count; i++)
                {
                    ItemSpell spell = spells[i];
                    if (!spell.Is(FunctionType.ChangeVariable)
                        || !spell.TryReadInt(0, out int statId) || !spell.TryReadInt(1, out int amount))
                        continue;

                    var stat = (CharacterStat)statId;
                    if (amount > 0 && DamageCalculator.IsAbsorbStat(stat))
                        yield return (stat, amount);
                }
            }
        }

        /// <summary>What is left of this buff's grant to <paramref name="stat"/>.</summary>
        public int AbsorbLeft(CharacterStat stat)
            => _absorbLeft != null && _absorbLeft.TryGetValue(stat, out int left) ? left : 0;

        internal void SetAbsorbLeft(CharacterStat stat, int left)
        {
            _absorbLeft ??= new Dictionary<CharacterStat, int>();
            _absorbLeft[stat] = Math.Max(0, left);
        }

        /// <summary>Takes up to <paramref name="amount"/> off what this buff has left for <paramref name="stat"/>; returns what it took.</summary>
        internal int SpendAbsorb(CharacterStat stat, int amount)
        {
            int left = AbsorbLeft(stat);
            int spent = Math.Min(left, amount);
            if (spent > 0)
                _absorbLeft![stat] = left - spent;
            return spent;
        }

        /// <summary>What is left of each absorb layer this buff granted, by stat. Null until it grants one.</summary>
        Dictionary<CharacterStat, int>? _absorbLeft;

        /// <summary>Functions to run once when the buff leaves NCU.</summary>
        public IReadOnlyList<ItemSpell> TerminateSpells
            => SpellList.TryGetValue(EventType.OnTerminate, out List<ItemSpell>? spells) ? spells : [];

        public static Buff Create(NanoSpell spell, Identity source, int nanoInstance, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(spell);

            if (!spell.IsBuff)
            {
                throw new InvalidOperationException(
                    "Nano " + spell.Id + " is not a buff and cannot occupy NCU.");
            }

            var buff = new Buff(spell, source, nanoInstance, nowUtc.AddMilliseconds(ClampedDuration(spell) * 10L));
            // The land runs the initial hit.
            buff.SchedulePeriodic(nowUtc, nowUtc, hitsAlreadyDone: 1);
            return buff;
        }

        /// <summary>
        /// Rebuilds a persisted buff. The stored deadline is kept as-is, so time spent offline
        /// counts against the buff instead of being refunded on login.
        /// </summary>
        public static Buff Restore(NanoSpell spell, Identity source, int nanoInstance, DateTime expiresAtUtc)
        {
            ArgumentNullException.ThrowIfNull(spell);
            var buff = new Buff(spell, source, nanoInstance, expiresAtUtc);
            // Ticks restart from now; the expiry cut in CollectDueTicks keeps them inside what is left. A delayed single
            // tick keeps its place before the stored expiry (a proc buff's self-recast stays 5 s before its end), and one
            // whose time passed while offline runs on the first tick.
            DateTime landedUtc = expiresAtUtc.AddMilliseconds(-ClampedDuration(spell) * 10L);
            buff.SchedulePeriodic(DateTime.UtcNow, landedUtc, hitsAlreadyDone: 1);
            return buff;
        }

        static int ClampedDuration(NanoSpell spell) => Math.Clamp(spell.DurationCentiseconds, 1, MaxDurationCentiseconds);

        public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;

        /// <summary>Remaining lifetime in centiseconds, floored at 0.</summary>
        public int RemainingCentiseconds(DateTime nowUtc)
        {
            double remaining = (ExpiresAtUtc - nowUtc).TotalMilliseconds / 10.0;
            if (remaining <= 0)
                return 0;

            return remaining >= MaxDurationCentiseconds ? MaxDurationCentiseconds : (int)remaining;
        }

        /// <summary>
        /// True when this buff may leave for <paramref name="reason"/>. Only an owner-driven
        /// cancel can be refused; expiry, replacement, death and playfield exit always win.
        /// </summary>
        public bool TryCancel(BuffRemovalReason reason)
            => reason != BuffRemovalReason.Cancelled || CanCancel;
    }
}
