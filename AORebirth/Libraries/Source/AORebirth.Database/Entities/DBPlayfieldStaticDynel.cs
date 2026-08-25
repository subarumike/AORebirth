namespace AORebirth.Database.Entities
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Database.Dao;

    #endregion

    /// <summary>
    /// Static dynel (door, object, etc) definition.
    /// </summary>
    [Tablename("playfield_static_dynels")]
    public class DBPlayfieldStaticDynel : IDBEntity
    {
		public int Id { get; set; }
		
        /// <summary>
        /// Static dynel ID.
        /// </summary>
        public int StaticDynelId { get; set; }

        /// <summary>
        /// Playfield ID.
        /// </summary>
        public int PlayfieldId { get; set; }

        /// <summary>
        /// Dynel type (door, object, etc).
        /// </summary>
        public string DynelType { get; set; }

        /// <summary>
        /// Position X.
        /// </summary>
        public float PositionX { get; set; }

        /// <summary>
        /// Position Y.
        /// </summary>
        public float PositionY { get; set; }

        /// <summary>
        /// Position Z.
        /// </summary>
        public float PositionZ { get; set; }

        /// <summary>
        /// Orientation X.
        /// </summary>
        public float OrientationX { get; set; }

        /// <summary>
        /// Orientation Y.
        /// </summary>
        public float OrientationY { get; set; }

        /// <summary>
        /// Orientation Z.
        /// </summary>
        public float OrientationZ { get; set; }

        /// <summary>
        /// Orientation W.
        /// </summary>
        public float OrientationW { get; set; }

        /// <summary>
        /// Mesh ID.
        /// </summary>
        public int MeshId { get; set; }

        /// <summary>
        /// Visual info (JSON).
        /// </summary>
        public string VisualInfo { get; set; }

        /// <summary>
        /// State (JSON).
        /// </summary>
        public string StateJson { get; set; }

        /// <summary>
        /// Whether active.
        /// </summary>
        public bool Enabled { get; set; }
    }
}