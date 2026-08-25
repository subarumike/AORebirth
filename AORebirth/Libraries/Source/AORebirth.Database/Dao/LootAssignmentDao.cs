namespace AORebirth.Database.Dao
{
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Database.Entities;

    /// <summary>
    /// DAO pour assignations loot.
    /// </summary>
    public class LootAssignmentDao : Dao<DBLootAssignment, LootAssignmentDao>
    {
        public static LootAssignmentDao Instance => new LootAssignmentDao();

        public DBLootAssignment GetByKey(string assignmentKey)
        {
            return this.GetWhere(new { AssignmentKey = assignmentKey }).FirstOrDefault();
        }

        public IEnumerable<DBLootAssignment> GetByTargetType(string targetType)
        {
            return this.GetWhere(new { TargetType = targetType, Enabled = true });
        }

        public IEnumerable<DBLootAssignment> GetByTargetKey(string targetKey)
        {
            return this.GetWhere(new { TargetKey = targetKey, Enabled = true });
        }

        public IEnumerable<DBLootAssignment> GetByPlayfieldId(int playfieldId)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId, Enabled = true });
        }

        public IEnumerable<DBLootAssignment> GetGlobal()
        {
            return this.GetWhere(new { PlayfieldId = (int?)null, Enabled = true });
        }
    }
}
