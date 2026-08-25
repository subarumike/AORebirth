namespace AORebirth.Database.Entities
{
	
    using System.Collections.Generic;

    using AORebirth.Database.Dao;	
    
	[Tablename("ordinary_enemy_profiles")]
    public class DBOrdinaryEnemyProfile : IDBEntity
    {
		public int Id { get; set; }
        public string ProfileKey { get; set; }
        public int MonsterData { get; set; }
        public string EnemyName { get; set; }
        public string FamilyKey { get; set; }
        public string AggressionMode { get; set; }
        public float? AggressionRadius { get; set; }
        public bool AutoAggro { get; set; }
        public bool SocialAggro { get; set; }
        public float? SocialAggroRadius { get; set; }
        public string CorpseProfileKey { get; set; }
        public string EvidenceState { get; set; }
        public int? PlayfieldId { get; set; }
        public string LootTableKey { get; set; }  // NEW
        public bool Enabled { get; set; }
    }
}