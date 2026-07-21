#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace AORebirth.Database.Dao
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;

    using Dapper;

    using Utility;

    /// <summary>
    /// GMI personal vault persistence (gmi_vault / gmi_vault_item).
    /// </summary>
    public static class GmiVaultDao
    {
        public sealed class VaultItemRow
        {
            public int LowId { get; set; }

            public int HighId { get; set; }

            public int Quality { get; set; }

            public int StackCount { get; set; }

            public int Icon { get; set; }

            public string ItemName { get; set; }

            public short SlotIndex { get; set; }
        }

        public sealed class VaultSnapshot
        {
            public int CharacterId { get; set; }

            public string CharacterName { get; set; }

            public long Credits { get; set; }

            public List<VaultItemRow> Items { get; set; }

            public VaultSnapshot()
            {
                this.Items = new List<VaultItemRow>();
                this.CharacterName = string.Empty;
            }
        }

        public static VaultSnapshot Load(int characterId)
        {
            var snap = new VaultSnapshot { CharacterId = characterId };
            if (characterId <= 0)
            {
                return snap;
            }

            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    var head = conn.Query(
                        "SELECT character_id AS CharacterId, character_name AS CharacterName, credits AS Credits FROM gmi_vault WHERE character_id=@id LIMIT 1",
                        new { id = characterId }).FirstOrDefault();
                    if (head == null)
                    {
                        return snap;
                    }

                    snap.CharacterId = (int)head.CharacterId;
                    snap.CharacterName = head.CharacterName != null ? (string)head.CharacterName : string.Empty;
                    snap.Credits = (long)head.Credits;
                    snap.Items = conn.Query<VaultItemRow>(
                        @"SELECT low_id AS LowId, high_id AS HighId, quality AS Quality,
                                 stack_count AS StackCount, icon AS Icon, item_name AS ItemName,
                                 slot_index AS SlotIndex
                          FROM gmi_vault_item WHERE character_id=@id
                          ORDER BY slot_index ASC, id ASC",
                        new { id = characterId }).ToList();
                    return snap;
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                return snap;
            }
        }

        public static bool Save(int characterId, string characterName, long credits, IList<VaultItemRow> items)
        {
            if (characterId <= 0)
            {
                return false;
            }

            if (characterName == null)
            {
                characterName = string.Empty;
            }

            if (items == null)
            {
                items = new List<VaultItemRow>();
            }

            try
            {
                using (IDbConnection conn = Connector.GetConnection())
                {
                    using (IDbTransaction trans = conn.BeginTransaction())
                    {
                        conn.Execute(
                            @"INSERT INTO gmi_vault (character_id, character_name, credits)
                              VALUES (@id, @name, @credits)
                              ON DUPLICATE KEY UPDATE character_name=VALUES(character_name), credits=VALUES(credits)",
                            new { id = characterId, name = characterName, credits = credits },
                            trans);

                        conn.Execute(
                            "DELETE FROM gmi_vault_item WHERE character_id=@id",
                            new { id = characterId },
                            trans);

                        short slot = 0;
                        foreach (VaultItemRow item in items)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            conn.Execute(
                                @"INSERT INTO gmi_vault_item
                                  (character_id, low_id, high_id, quality, stack_count, icon, item_name, slot_index)
                                  VALUES (@cid, @low, @high, @ql, @cnt, @icon, @iname, @slot)",
                                new
                                {
                                    cid = characterId,
                                    low = item.LowId,
                                    high = item.HighId,
                                    ql = item.Quality,
                                    cnt = item.StackCount,
                                    icon = item.Icon,
                                    iname = item.ItemName ?? string.Empty,
                                    slot = slot
                                },
                                trans);
                            slot++;
                        }

                        trans.Commit();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorException(e);
                return false;
            }
        }
    }
}
