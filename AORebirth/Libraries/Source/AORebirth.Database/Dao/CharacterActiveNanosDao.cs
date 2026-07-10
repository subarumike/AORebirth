#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 

#endregion

namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Database.Entities;

    #endregion

    public class CharacterActiveNanosDao : Dao<DBCharacterActiveNano, CharacterActiveNanosDao>
    {
        public bool HasActiveNanos(int characterId)
        {
            return this.GetAll(new { CharacterId = characterId }).Any();
        }

        public List<DBCharacterActiveNano> ReadActiveNanos(int characterId)
        {
            return this.GetAll(new { CharacterId = characterId }).ToList();
        }

        public void ReplaceActiveNanos(int characterId, IEnumerable<DBCharacterActiveNano> rows)
        {
            this.Delete(new { CharacterId = characterId });

            if (rows == null)
            {
                return;
            }

            foreach (DBCharacterActiveNano row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                row.CharacterId = characterId;
                this.Add(row);
            }
        }

        public void DeleteExpiredActiveNanos(int characterId, DateTime nowUtc)
        {
            List<DBCharacterActiveNano> activeRows = this.ReadActiveNanos(characterId);
            foreach (DBCharacterActiveNano row in activeRows)
            {
                if (row.ExpiresAtUtcTicks <= 0)
                {
                    continue;
                }

                DateTime expiresAtUtc = new DateTime(row.ExpiresAtUtcTicks, DateTimeKind.Utc);
                if (expiresAtUtc <= nowUtc)
                {
                    this.Delete(row.Id);
                }
            }
        }
    }
}
