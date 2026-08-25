namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Unified static dynel loader.
    /// </summary>
    public class PlayfieldStaticDynelDao : Dao<DBPlayfieldStaticDynel, PlayfieldStaticDynelDao>
    {
        public static PlayfieldStaticDynelDao Instance => new PlayfieldStaticDynelDao();

        public IEnumerable<DBPlayfieldStaticDynel> GetByPlayfieldId(int playfieldId)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId, Enabled = true });
        }

        public IEnumerable<DBPlayfieldStaticDynel> GetByPlayfieldAndType(int playfieldId, string dynelType)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId, DynelType = dynelType, Enabled = true });
        }

        public IEnumerable<DBPlayfieldStaticDynel> GetAllEnabled()
        {
            return this.GetWhere(new { Enabled = true });
        }
    }
}
