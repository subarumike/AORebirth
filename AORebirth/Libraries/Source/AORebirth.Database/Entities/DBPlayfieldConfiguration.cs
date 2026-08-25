namespace AORebirth.Database.Entities
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Database.Dao;

    #endregion

    /// <summary>
    /// Playfield configuration metadata.
    /// </summary>
    [Tablename("playfield_configurations")]
    public class DBPlayfieldConfiguration : IDBEntity
    {
		public int Id { get; set; }
		
        /// <summary>
        /// Playfield ID (primary key).
        /// </summary>
        public int PlayfieldId { get; set; }

        /// <summary>
        /// Human-readable playfield name.
        /// </summary>
        public string PlayfieldName { get; set; }

        /// <summary>
        /// Geometry resource ID (for statels/collision).
        /// </summary>
        public int? GeometryResourceId { get; set; }

        /// <summary>
        /// Content profile key (references playfield_content_profiles).
        /// </summary>
        public string ContentProfileKey { get; set; }

        /// <summary>
        /// Loot profile key or global reference.
        /// </summary>
        public string LootProfileKey { get; set; }

        /// <summary>
        /// Is this playfield instanced (dungeon, mission, etc.).
        /// </summary>
        public bool IsInstanced { get; set; }

        /// <summary>
        /// Maximum concurrent instances (null = infinite).
        /// </summary>
        public int? MaxInstances { get; set; }

        /// <summary>
        /// Respawn policy key.
        /// </summary>
        public string RespawnPolicyKey { get; set; }

        /// <summary>
        /// Whether this configuration is active.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Description/notes.
        /// </summary>
        public string Description { get; set; }
    }
}