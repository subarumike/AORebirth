namespace AORebirth.Database.Entities
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Database.Dao;

    #endregion

    /// <summary>
    /// Single segment of a patrol route.
    /// </summary>
    [Tablename("npc_patrol_segments")]
    public class DBNpcPatrolSegment : IDBEntity
    {
		public int Id { get; set; }
		
        /// <summary>
        /// Segment ID.
        /// </summary>
        public int SegmentId { get; set; }

        /// <summary>
        /// Route ID (references npc_patrol_routes).
        /// </summary>
        public int RouteId { get; set; }

        /// <summary>
        /// Segment order index.
        /// </summary>
        public int SegmentIndex { get; set; }

        /// <summary>
        /// Duration in seconds.
        /// </summary>
        public float DurationSeconds { get; set; }

        /// <summary>
        /// Start position X.
        /// </summary>
        public float StartX { get; set; }

        /// <summary>
        /// Start position Y.
        /// </summary>
        public float StartY { get; set; }

        /// <summary>
        /// Start position Z.
        /// </summary>
        public float StartZ { get; set; }

        /// <summary>
        /// End position X.
        /// </summary>
        public float EndX { get; set; }

        /// <summary>
        /// End position Y.
        /// </summary>
        public float EndY { get; set; }

        /// <summary>
        /// End position Z.
        /// </summary>
        public float EndZ { get; set; }

        /// <summary>
        /// Movement speed per second.
        /// </summary>
        public float SpeedPerSecond { get; set; }

        /// <summary>
        /// Animation key (if any).
        /// </summary>
        public int AnimationKey { get; set; }
    }
}