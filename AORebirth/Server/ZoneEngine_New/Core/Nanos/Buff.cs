namespace ZoneEngine_New.Core.Nanos
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

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

        /// <summary>
        /// Heal/damage over time: an OnUse Hit with TickCount &gt; 1 and a TickInterval (centiseconds).
        /// The land runs it once; the rest run every interval while the buff is up, up to TickCount in all.
        /// </summary>
        void SchedulePeriodic(DateTime nowUtc, int hitsAlreadyDone)
        {
            _periodic.Clear();
            if (!SpellList.TryGetValue(EventType.OnUse, out List<ItemSpell>? spells))
                return;

            for (int i = 0; i < spells.Count; i++)
            {
                ItemSpell spell = spells[i];
                if (!spell.Is(FunctionType.Hit) || spell.TickCount <= 1 || spell.TickInterval == 0)
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

        public DateTime ExpiresAtUtc { get; }

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

            int duration = Math.Clamp(spell.DurationCentiseconds, 1, MaxDurationCentiseconds);
            var buff = new Buff(spell, source, nanoInstance, nowUtc.AddMilliseconds(duration * 10L));
            // The land runs the initial hit.
            buff.SchedulePeriodic(nowUtc, hitsAlreadyDone: 1);
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
            // Ticks restart from now; the expiry cut in CollectDueTicks keeps them inside what is left.
            buff.SchedulePeriodic(DateTime.UtcNow, hitsAlreadyDone: 1);
            return buff;
        }

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
