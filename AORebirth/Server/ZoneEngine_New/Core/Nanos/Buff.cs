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

        Buff(NanoSpell spell, Identity source, int nanoInstance, DateTime expiresAtUtc)
            : base(spell)
        {
            Source = source;
            NanoInstance = nanoInstance;
            ExpiresAtUtc = expiresAtUtc;
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

        /// <summary>Functions to run once when the buff leaves NCU. Not executed yet.</summary>
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
            return new Buff(spell, source, nanoInstance, nowUtc.AddMilliseconds(duration * 10L));
        }

        /// <summary>
        /// Rebuilds a persisted buff. The stored deadline is kept as-is, so time spent offline
        /// counts against the buff instead of being refunded on login.
        /// </summary>
        public static Buff Restore(NanoSpell spell, Identity source, int nanoInstance, DateTime expiresAtUtc)
        {
            ArgumentNullException.ThrowIfNull(spell);
            return new Buff(spell, source, nanoInstance, expiresAtUtc);
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
