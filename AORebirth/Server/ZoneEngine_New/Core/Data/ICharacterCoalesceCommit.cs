namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    /// <summary>
    /// One MySQL transaction for dirty inventory rows and newly uploaded nanos.
    /// </summary>
    public interface ICharacterCoalesceCommit
    {
        void Persist(
            IReadOnlyList<ItemInstanceRecord> inserts,
            IReadOnlyList<ItemLocationUpdate> updates,
            int characterId,
            IReadOnlyList<int> uploadedNanoIds);
    }
}
