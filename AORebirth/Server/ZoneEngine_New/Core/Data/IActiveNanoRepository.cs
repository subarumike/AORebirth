namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    /// <summary>One persisted NCU entry. Expiry is stored absolute so a relog cannot refresh it.</summary>
    public sealed class ActiveNanoRecord
    {
        public int NanoId { get; init; }

        public int Strain { get; init; }

        public int NanoInstance { get; init; }

        public int DurationCentiseconds { get; init; }

        public long ExpiresAtUtcTicks { get; init; }
    }

    public interface IActiveNanoRepository
    {
        IReadOnlyList<ActiveNanoRecord> GetForCharacter(int characterId);
    }
}
