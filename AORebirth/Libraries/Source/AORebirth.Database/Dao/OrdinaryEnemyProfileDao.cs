namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Unified enemy profile loader (replaces hardcoded OrdinaryEnemyCatalog).
    /// Supports Subway, Temple3W, custom playfields.
    /// </summary>
    public class OrdinaryEnemyProfileDao : Dao<DBOrdinaryEnemyProfile, OrdinaryEnemyProfileDao>
    {
        public static OrdinaryEnemyProfileDao Instance => new OrdinaryEnemyProfileDao();

        public DBOrdinaryEnemyProfile GetByProfileKey(string profileKey)
        {
            if (string.IsNullOrWhiteSpace(profileKey))
            {
                return null;
            }

            return this.GetWhere(new { ProfileKey = profileKey }).FirstOrDefault();
        }

        public IEnumerable<DBOrdinaryEnemyProfile> GetByPlayfieldId(int playfieldId)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId, Enabled = true });
        }

        public IEnumerable<DBOrdinaryEnemyProfile> GetByFamilyKey(string familyKey)
        {
            if (string.IsNullOrWhiteSpace(familyKey))
            {
                return Enumerable.Empty<DBOrdinaryEnemyProfile>();
            }

            return this.GetWhere(new { FamilyKey = familyKey, Enabled = true });
        }

        public IEnumerable<DBOrdinaryEnemyProfile> GetAllEnabled()
        {
            return this.GetWhere(new { Enabled = true });
        }
    }
}
