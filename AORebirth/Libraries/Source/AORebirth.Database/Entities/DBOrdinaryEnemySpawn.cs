namespace AORebirth.Database.Entities
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Database.Dao;

    #endregion

    /// <summary>
    /// Ordinary enemy spawn definition (position, patrol, level, etc).
    /// </summary>
    [Tablename("ordinary_enemy_spawns")]
    public class DBOrdinaryEnemySpawn : IDBEntity
    {
		public int Id { get; set; }
		
        /// <summary>
        /// Spawn ID (auto-increment).
        /// </summary>
        public int SpawnId { get; set; }

        /// <summary>
        /// Playfield ID.
        /// </summary>
        public int PlayfieldId { get; set; }

        /// <summary>
        /// Profile key (references ordinary_enemy_profiles).
        /// </summary>
        public string ProfileKey { get; set; }

        /// <summary>
        /// Unique spawn key for audit/reference.
        /// </summary>
        public string SpawnKey { get; set; }

        /// <summary>
        /// World position X.
        /// </summary>
        public float PositionX { get; set; }

        /// <summary>
        /// World position Y.
        /// </summary>
        public float PositionY { get; set; }

        /// <summary>
        /// World position Z.
        /// </summary>
        public float PositionZ { get; set; }

        /// <summary>
        /// Orientation quaternion X.
        /// </summary>
        public float OrientationX { get; set; }

        /// <summary>
        /// Orientation quaternion Y.
        /// </summary>
        public float OrientationY { get; set; }

        /// <summary>
        /// Orientation quaternion Z.
        /// </summary>
        public float OrientationZ { get; set; }

        /// <summary>
        /// Orientation quaternion W.
        /// </summary>
        public float OrientationW { get; set; }

        /// <summary>
        /// Level definition key ("fixed:10" or "band:5-15").
        /// </summary>
        public string LevelDefinitionKey { get; set; }

        /// <summary>
        /// Minimum level (if applicable).
        /// </summary>
        public int? MinLevel { get; set; }

        /// <summary>
        /// Maximum level (if applicable).
        /// </summary>
        public int? MaxLevel { get; set; }

        /// <summary>
        /// Respawn interval in seconds.
        /// </summary>
        public float RespawnSeconds { get; set; }

        /// <summary>
        /// Patrol route ID (references npc_patrol_routes).
        /// </summary>
        public int? PatrolRouteId { get; set; }

        /// <summary>
        /// Initial health damage (for partial-spawn scenarios).
        /// </summary>
        public int HealthDamage { get; set; }

        /// <summary>
        /// Whether to use spawn location as patrol start.
        /// </summary>
        public bool UseSpawnAsPatrolStart { get; set; }

        /// <summary>
        /// Loot table key override (if spawn-specific loot).
        /// </summary>
        public string LootTableKeyOverride { get; set; }

        /// <summary>
        /// Whether this spawn is active.
        /// </summary>
        public bool Enabled { get; set; }
    }
}