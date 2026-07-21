#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace AORebirth.Database.Dao
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;

    using AORebirth.Database.Entities;

    using Dapper;

    using Utility;

    #endregion

    /// <summary>
    /// Persistence for character trained perk PacketIDs.
    /// </summary>
    public class CharacterPerksDao : Dao<DBCharacterPerk, CharacterPerksDao>
    {
        private static readonly object TableSync = new object();

        private static bool tableEnsured;

        private const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS `charactersperks` ("
            + "`Id` int(32) NOT NULL AUTO_INCREMENT,"
            + "`CharacterId` int(32) NOT NULL,"
            + "`PacketId` int(11) NOT NULL,"
            + "PRIMARY KEY (`Id`),"
            + "INDEX `Perks` (`CharacterId`, `PacketId`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=latin1;";

        /// <summary>
        /// Ensures the charactersperks table exists (safe for existing DBs that never got the Y prompt).
        /// </summary>
        public void EnsureTable()
        {
            if (tableEnsured)
            {
                return;
            }

            lock (TableSync)
            {
                if (tableEnsured)
                {
                    return;
                }

                try
                {
                    using (IDbConnection conn = Connector.GetConnection())
                    {
                        conn.Execute(CreateTableSql);
                    }

                    tableEnsured = true;
                }
                catch (Exception ex)
                {
                    LogUtil.ErrorException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="charId">
        /// </param>
        /// <returns>
        /// </returns>
        public IEnumerable<int> ReadPacketIds(int charId)
        {
            this.EnsureTable();
            return this.GetAll(new { CharacterId = charId }).Select(x => x.PacketId).ToList();
        }

        /// <summary>
        /// Inserts one trained perk if not already stored.
        /// </summary>
        /// <param name="charId">
        /// </param>
        /// <param name="packetId">
        /// </param>
        public void WritePerk(int charId, int packetId)
        {
            this.EnsureTable();
            if (!this.ReadPacketIds(charId).Contains(packetId))
            {
                this.Add(new DBCharacterPerk { CharacterId = charId, PacketId = packetId });
            }
        }

        /// <summary>
        /// Inserts any trained PacketIDs not already stored for the character.
        /// </summary>
        /// <param name="charId">
        /// </param>
        /// <param name="packetIds">
        /// </param>
        public void WritePerks(int charId, IEnumerable<int> packetIds)
        {
            this.EnsureTable();
            if (packetIds == null)
            {
                return;
            }

            List<int> existing = this.ReadPacketIds(charId).ToList();
            foreach (int packetId in packetIds)
            {
                if (!existing.Contains(packetId))
                {
                    this.Add(new DBCharacterPerk { CharacterId = charId, PacketId = packetId });
                    existing.Add(packetId);
                }
            }
        }

        /// <summary>
        /// Removes one trained perk row (future RemovePerk / re-perk).
        /// </summary>
        /// <param name="charId">
        /// </param>
        /// <param name="packetId">
        /// </param>
        public void DeletePerk(int charId, int packetId)
        {
            this.EnsureTable();
            this.Delete(new { CharacterId = charId, PacketId = packetId });
        }

        /// <summary>
        /// Removes all trained perk rows for a character (full perk reset).
        /// </summary>
        /// <param name="charId">
        /// </param>
        public void DeleteAllPerks(int charId)
        {
            this.EnsureTable();
            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    conn.Execute(
                        "DELETE FROM `charactersperks` WHERE `CharacterId` = @CharacterId",
                        new { CharacterId = charId });
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
                throw;
            }
        }
    }
}
