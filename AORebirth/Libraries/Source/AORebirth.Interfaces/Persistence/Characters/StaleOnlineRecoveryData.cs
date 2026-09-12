namespace AORebirth.Interfaces.Persistence.Characters
{
    using System;
    using System.Collections.Generic;

    /// <summary>Detached recovery evidence. A zero post-count is true only at verification time.</summary>
    public sealed class StaleOnlineRecoveryData
    {
        public StaleOnlineRecoveryData(
            string databaseName, IEnumerable<StaleOnlineCharacterData> rows,
            int rowsUpdated, long? postUpdateNonzeroCount)
        {
            if (rows == null)
            {
                throw new ArgumentNullException("rows");
            }

            this.DatabaseName = databaseName;
            this.Rows = new List<StaleOnlineCharacterData>(rows).AsReadOnly();
            this.RowsUpdated = rowsUpdated;
            this.PostUpdateNonzeroCount = postUpdateNonzeroCount;
        }

        public string DatabaseName { get; private set; }
        public IReadOnlyList<StaleOnlineCharacterData> Rows { get; private set; }
        public int RowsUpdated { get; private set; }

        /// <summary>Null means the empty-capture path did not require a post-update count.</summary>
        public long? PostUpdateNonzeroCount { get; private set; }
        public bool CleanupRequired { get { return this.Rows.Count != 0; } }
    }
}
