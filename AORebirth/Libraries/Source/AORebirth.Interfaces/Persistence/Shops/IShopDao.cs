namespace AORebirth.Interfaces.Persistence.Shops
{
    using System.Collections.Generic;

    /// <summary>
    /// Read-only vendor definitions. Implementations own all database access and return detached data.
    /// </summary>
    public interface IShopDao
    {
        /// <summary>Returns every database vendor definition for one playfield, ordered by vendor and stock-row identity.</summary>
        IList<ShopVendorData> ListForPlayfield(int playfieldId);
    }
}
