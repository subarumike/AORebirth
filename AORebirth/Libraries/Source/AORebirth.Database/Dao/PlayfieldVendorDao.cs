namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Unified vendor loader (replaces statel parsing).
    /// </summary>
    public class PlayfieldVendorDao : Dao<DBPlayfieldVendor, PlayfieldVendorDao>
    {
        public static PlayfieldVendorDao Instance => new PlayfieldVendorDao();

        public IEnumerable<DBPlayfieldVendor> GetByPlayfieldId(int playfieldId)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId, Enabled = true });
        }

        public DBPlayfieldVendor GetByVendorId(int vendorId)
        {
            return this.GetWhere(new { VendorId = vendorId }).FirstOrDefault();
        }

        public IEnumerable<DBPlayfieldVendor> GetAllEnabled()
        {
            return this.GetWhere(new { Enabled = true });
        }
    }
}
