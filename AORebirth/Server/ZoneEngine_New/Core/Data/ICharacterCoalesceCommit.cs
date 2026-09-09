namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    /// <summary>
    /// One MySQL transaction for dirty inventory rows, newly uploaded nanos and NCU.
    /// </summary>
    public interface ICharacterCoalesceCommit
    {
        /// <summary>
        /// <paramref name="activeNanos"/> null means NCU did not change and must not be touched;
        /// a non-null list (including an empty one) replaces the character's stored NCU.
        /// </summary>
        void Persist(
            IReadOnlyList<ItemInstanceRecord> inserts,
            IReadOnlyList<ItemLocationUpdate> updates,
            int characterId,
            IReadOnlyList<int> uploadedNanoIds,
            IReadOnlyList<ActiveNanoRecord>? activeNanos);
    }
}
