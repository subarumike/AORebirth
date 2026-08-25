namespace AORebirth.Database.Entities
{
    using System.Data.Linq;
	
    using System.Collections.Generic;

    using AORebirth.Database.Dao;	

    [Tablename("loot_table_definitions")]
    public class DBLootTableDefinition : IDBEntity
    {
		public int Id { get; set; }
        public string LootTableKey { get; set; }
        public string DisplayName { get; set; }
        public string TableType { get; set; }
        public string RollMode { get; set; }
        public string CreditsPolicyMode { get; set; }
        public int CreditsMin { get; set; }
        public int CreditsMax { get; set; }
        public string CreditsObservedJson { get; set; }
        public string QualityPolicy { get; set; }
        public bool ItemPoolUnresolved { get; set; }
        public string EvidenceJson { get; set; }
        public string Confidence { get; set; }
        public bool Enabled { get; set; }
        public int? PlayfieldId { get; set; }
    }
}