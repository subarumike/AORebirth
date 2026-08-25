namespace AORebirth.Database.Entities
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Database.Dao;

    #endregion

    /// <summary>
    /// Patrol route definition.
    /// </summary>
    [Tablename("npc_patrol_routes")]
    public class DBNpcPatrolRoute : IDBEntity
    {
		public int Id { get; set; }
		
        /// <summary>
        /// Route ID.
        /// </summary>
        public int RouteId { get; set; }

        /// <summary>
        /// Playfield ID.
        /// </summary>
        public int PlayfieldId { get; set; }

        /// <summary>
        /// Unique route key.
        /// </summary>
        public string RouteKey { get; set; }

        /// <summary>
        /// Use runtime start position instead of segment start.
        /// </summary>
        public bool UseRuntimeStart { get; set; }

        /// <summary>
        /// Batch zero-delay segments.
        /// </summary>
        public bool BatchZeroDelay { get; set; }

        /// <summary>
        /// Capture ID (audit trail).
        /// </summary>
        public string CreatedFromCaptureId { get; set; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; }
    }
}