namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One persisted NCU entry. <see cref="ExpiresAtUtcTicks"/> is either a UTC deadline (a buff saved while its owner
    /// was online, so a crash never refunds time) or, for a buff saved at logout, the lifetime it had left: buffs do
    /// not run while their owner is offline. A deadline is never before 2000, and a buff never has more than its
    /// 100-hour ceiling left, so the two cannot be mistaken for each other.
    /// </summary>
    public sealed class ActiveNanoRecord
    {
        static readonly long PausedLimitTicks = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        public int NanoId { get; init; }

        public int Strain { get; init; }

        public int NanoInstance { get; init; }

        public int DurationCentiseconds { get; init; }

        public long ExpiresAtUtcTicks { get; init; }

        internal bool HasValidPersistenceFields => NanoId > 0 && NanoInstance > 0 && Strain >= 0
            && DurationCentiseconds >= 0 && ExpiresAtUtcTicks >= 0 && ExpiresAtUtcTicks <= DateTime.MaxValue.Ticks;

        /// <summary>The stored value for a buff paused at logout with <paramref name="remaining"/> left.</summary>
        public static long Paused(TimeSpan remaining) => Math.Clamp(remaining.Ticks, 1, PausedLimitTicks - 1);

        /// <summary>When the buff runs out, counting a paused buff's remaining lifetime from <paramref name="nowUtc"/>.</summary>
        public DateTime ResolveExpiry(DateTime nowUtc) => ExpiresAtUtcTicks < PausedLimitTicks
            ? nowUtc + TimeSpan.FromTicks(ExpiresAtUtcTicks)
            : new DateTime(ExpiresAtUtcTicks, DateTimeKind.Utc);
    }

    public interface IActiveNanoRepository
    {
        IReadOnlyList<ActiveNanoRecord> GetForCharacter(int characterId);
    }
}
