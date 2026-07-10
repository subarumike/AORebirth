#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 

#endregion

namespace AORebirth.Database.Entities
{
    #region Usings ...

    using AORebirth.Database.Dao;

    #endregion

    [Tablename("charactersactivenanos")]
    public class DBCharacterActiveNano : IDBEntity
    {
        public int Id { get; set; }

        public int CharacterId { get; set; }

        public int NanoId { get; set; }

        public int Strain { get; set; }

        public int NanoInstance { get; set; }

        public int DurationCentiseconds { get; set; }

        public long ExpiresAtUtcTicks { get; set; }
    }
}
