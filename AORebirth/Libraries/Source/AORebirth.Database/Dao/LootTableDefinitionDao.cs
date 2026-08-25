namespace AORebirth.Database.Dao
{
    using System.Collections.Generic;
    using System.Linq;
    using AORebirth.Database.Entities;

    /// <summary>
    /// DAO pour tables loot (globales + playfield-spécifiques).
    /// </summary>
    public class LootTableDefinitionDao : Dao<DBLootTableDefinition, LootTableDefinitionDao>
    {
        public static LootTableDefinitionDao Instance => new LootTableDefinitionDao();

        public DBLootTableDefinition GetByKey(string lootTableKey)
        {
            return this.GetWhere(new { LootTableKey = lootTableKey }).FirstOrDefault();
        }

        public IEnumerable<DBLootTableDefinition> GetByPlayfieldId(int playfieldId)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId, Enabled = true });
        }

        public IEnumerable<DBLootTableDefinition> GetGlobal()
        {
            return this.GetWhere(new { PlayfieldId = (int?)null, Enabled = true });
        }

        public IEnumerable<DBLootTableDefinition> GetAllEnabled()
        {
            return this.GetWhere(new { Enabled = true });
        }
    }
}
