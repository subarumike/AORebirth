namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Unified enemy spawn loader (replaces OrdinaryEnemyCatalog.spawns[]).
    /// </summary>
    public class OrdinaryEnemySpawnDao : Dao<DBOrdinaryEnemySpawn, OrdinaryEnemySpawnDao>
    {
        public static OrdinaryEnemySpawnDao Instance => new OrdinaryEnemySpawnDao();

        public IEnumerable<DBOrdinaryEnemySpawn> GetByPlayfieldId(int playfieldId)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId, Enabled = true });
        }

        public DBOrdinaryEnemySpawn GetBySpawnId(int spawnId)
        {
            return this.GetWhere(new { SpawnId = spawnId }).FirstOrDefault();
        }

        public DBOrdinaryEnemySpawn GetBySpawnKey(string spawnKey)
        {
            if (string.IsNullOrWhiteSpace(spawnKey))
            {
                return null;
            }

            return this.GetWhere(new { SpawnKey = spawnKey }).FirstOrDefault();
        }

        public IEnumerable<DBOrdinaryEnemySpawn> GetAllEnabled()
        {
            return this.GetWhere(new { Enabled = true });
        }
    }
}
