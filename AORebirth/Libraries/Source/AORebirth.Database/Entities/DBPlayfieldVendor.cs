namespace AORebirth.Database.Entities
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Database.Dao;

    #endregion

    /// <summary>
    /// Vendor definition for a playfield.
    /// </summary>
    [Tablename("playfield_vendors")]
    public class DBPlayfieldVendor : IDBEntity
    {
		public int Id { get; set; }
		
        /// <summary>
        /// Vendor ID.
        /// </summary>
        public int VendorId { get; set; }

        /// <summary>
        /// Playfield ID.
        /// </summary>
        public int PlayfieldId { get; set; }

        /// <summary>
        /// Vendor template hash.
        /// </summary>
        public string VendorTemplateHash { get; set; }

        /// <summary>
        /// Vendor template ID (fallback).
        /// </summary>
        public int VendorTemplateId { get; set; }

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
        /// Vendor name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Sell modifier.
        /// </summary>
        public float SellModifier { get; set; }

        /// <summary>
        /// Buy modifier.
        /// </summary>
        public float BuyModifier { get; set; }

        /// <summary>
        /// Whether active.
        /// </summary>
        public bool Enabled { get; set; }
    }
}