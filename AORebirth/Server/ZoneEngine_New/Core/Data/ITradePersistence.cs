namespace ZoneEngine_New.Core.Data
{
    using System;
    using System.Collections.Generic;

    /// <summary>One durability boundary for every participant's items, nanos and final cash.</summary>
    public interface ITradePersistence
    {
        void Persist(TradePersistenceBatch batch);
    }

    public sealed record TradeCharacterWrite(int CharacterId, int Cash, IReadOnlyList<int> UploadedNanoIds);

    public sealed record TradePersistenceBatch(
        IReadOnlyList<ItemInstanceRecord> Inserts,
        IReadOnlyList<ItemLocationUpdate> Updates,
        IReadOnlyList<TradeCharacterWrite> Characters);

    /// <summary>
    /// The server did not receive COMMIT's outcome. Never retry or write a memory snapshot over
    /// the database: disconnect and quarantine the aggregates until they can be loaded afresh.
    /// </summary>
    public sealed class DatabaseCommitOutcomeUnknownException : Exception
    {
        public DatabaseCommitOutcomeUnknownException(Exception inner)
            : base("Database commit outcome is unknown; reconciliation is required.", inner) { }
    }
}
