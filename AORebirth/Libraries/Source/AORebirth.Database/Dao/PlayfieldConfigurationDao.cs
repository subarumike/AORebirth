namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// Unified playfield bootstrap configuration loader.
    /// Replaces 15+ ContentModule implementations.
    /// </summary>
    public class PlayfieldConfigurationDao : Dao<DBPlayfieldConfiguration, PlayfieldConfigurationDao>
    {
        public static PlayfieldConfigurationDao Instance => new PlayfieldConfigurationDao();

        public DBPlayfieldConfiguration GetByPlayfieldId(int playfieldId)
        {
            return this.GetWhere(new { PlayfieldId = playfieldId }).FirstOrDefault();
        }

        public IEnumerable<DBPlayfieldConfiguration> GetAllEnabled()
        {
            return this.GetWhere(new { Enabled = true });
        }
    }
}
