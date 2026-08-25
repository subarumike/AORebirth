namespace AORebirth.Database.Entities
{
	
    using System.Collections.Generic;

    using AORebirth.Database.Dao;
	
    [Tablename("loot_assignments")]
    public class DBLootAssignment : IDBEntity
    {
		public int Id { get; set; }
        public string AssignmentKey { get; set; }
        public string TargetType { get; set; }
        public string TargetKey { get; set; }
        public string LootTableKey { get; set; }
        public int? PlayfieldId { get; set; }
        public string EncounterKey { get; set; }
        public int? MinLevel { get; set; }
        public int? MaxLevel { get; set; }
        public int Priority { get; set; }
        public string ConditionsJson { get; set; }
        public string Evidence { get; set; }
        public string Confidence { get; set; }
        public bool Enabled { get; set; }
    }
}