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

namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Items;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    #endregion

    /// <summary>
    /// </summary>
    public class TradeSkill
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        private static TradeSkill instance;

        #endregion

        #region Fields

        /// <summary>
        /// </summary>
        public Dictionary<int, string> ItemNames = new Dictionary<int, string>();

        /// <summary>
        /// </summary>
        private readonly List<TradeSkillEntry> tradeSkillList = new List<TradeSkillEntry>();

        /// <summary>
        /// Fast Id1/Id2 lookup (High IDs as stored in tradeskill table).
        /// </summary>
        private readonly Dictionary<long, TradeSkillEntry> tradeSkillByPair =
            new Dictionary<long, TradeSkillEntry>();

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        static TradeSkill()
        {
        }

        /// <summary>
        /// </summary>
        private TradeSkill()
        {
            this.CacheItemNames();
            Console.WriteLine("Cached " + this.ItemNames.Count + " item names");
            this.CacheTradeSkills();
            Console.WriteLine("\rCached " + this.tradeSkillList.Count + " trade skill entries");
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// </summary>
        public static TradeSkill Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new TradeSkill();
                }

                return instance;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="lid">
        /// </param>
        /// <param name="hid">
        /// </param>
        /// <param name="ql">
        /// </param>
        /// <returns>
        /// </returns>
        public string GetItemName(int lid, int hid, int ql)
        {
            try
            {
                string lName = this.ItemNames[lid];
                string hName = this.ItemNames[hid];

                int lQL = ItemLoader.ItemList[lid].Quality;
                int hQL = ItemLoader.ItemList[hid].Quality;

                if (ql > (hQL - lQL) / 2 + lQL)
                {
                    return hName;
                }
                else
                {
                    return lName;
                }
            }
            catch (Exception)
            {
                return "NoName";
            }
        }

        /// <summary>
        /// Legacy exact HighID pair lookup (quest overrides + DB + Thrak). Prefer
        /// <see cref="ResolveTradeSkill"/> for preview/build so reverse implant drag works.
        /// </summary>
        public TradeSkillEntry GetTradeSkillEntry(int id1, int id2)
        {
            TradeSkillMatch match = this.ResolveTradeSkill(id1, id2, id1, id2);
            return match != null ? match.Entry : null;
        }

        /// <summary>
        /// Resolve a tradeskill recipe for two inventory items.
        /// Tries quest overrides, then DB High/High, reverse, and Low/Low / mixed IDs
        /// (Overflow intermediates often use low==high while DB stores the high AOID).
        /// </summary>
        public TradeSkillMatch ResolveTradeSkill(
            int sourceLowId,
            int sourceHighId,
            int targetLowId,
            int targetHighId)
        {
            TradeSkillEntry entry =
                ZoneEngine.Core.Arete.Quests.PersonalizedRobotBrainCombineRules.TryMatch(
                    sourceHighId,
                    targetHighId);
            if (entry != null)
            {
                return new TradeSkillMatch { Entry = entry, Swapped = false };
            }

            entry = ZoneEngine.Core.Arete.Quests.VernonGodfrayCombineRules.TryMatch(sourceHighId, targetHighId);
            if (entry != null)
            {
                return new TradeSkillMatch { Entry = entry, Swapped = false };
            }

            // Capture 20260721-Mason: Arete tip Overflow QL1 (handles reverse itself).
            entry = ZoneEngine.Core.Arete.Quests.DoctorMasonCombineRules.TryMatch(sourceHighId, targetHighId);
            if (entry != null)
            {
                return new TradeSkillMatch { Entry = entry, Swapped = false };
            }

            entry = ZoneEngine.Core.Arete.Quests.LoreleiCombineRules.TryMatch(sourceHighId, targetHighId);
            if (entry != null)
            {
                return new TradeSkillMatch { Entry = entry, Swapped = false };
            }

            TradeSkillMatch dbMatch = this.TryResolveDbPair(
                sourceLowId,
                sourceHighId,
                targetLowId,
                targetHighId);
            if (dbMatch != null)
            {
                return dbMatch;
            }

            entry = ZoneEngine.Core.Thrak.Quests.ThrakGardenKeyCombineRules.TryMatch(sourceHighId, targetHighId);
            if (entry != null)
            {
                return new TradeSkillMatch { Entry = entry, Swapped = false };
            }

            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="id">
        /// </param>
        /// <returns>
        /// </returns>
        public int SourceProcessesCount(int id)
        {
            return this.SourceProcessesCount(id, id);
        }

        /// <summary>
        /// Count recipes where this item can be Source (DB Id1, or Id2 when reverse-drag implant).
        /// </summary>
        public int SourceProcessesCount(int lowId, int highId)
        {
            int[] ids = ExpandTradeSkillIds(lowId, highId);
            int count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                int id = ids[i];
                count += this.tradeSkillList.Count(x => x.ID1 == id || (x.IsImplant && x.ID2 == id));
            }

            // Quest overrides still key off HighID.
            int high = highId > 0 ? highId : lowId;
            return count
                   + ZoneEngine.Core.Arete.Quests.PersonalizedRobotBrainCombineRules.SourceProcessBonus(high)
                   + ZoneEngine.Core.Arete.Quests.VernonGodfrayCombineRules.SourceProcessBonus(high)
                   + ZoneEngine.Core.Arete.Quests.DoctorMasonCombineRules.SourceProcessBonus(high)
                   + ZoneEngine.Core.Arete.Quests.LoreleiCombineRules.SourceProcessBonus(high);
        }

        /// <summary>
        /// </summary>
        /// <param name="id">
        /// </param>
        /// <returns>
        /// </returns>
        public int TargetProcessesCount(int id)
        {
            return this.TargetProcessesCount(id, id);
        }

        /// <summary>
        /// Count recipes where this item can be Target (DB Id2, or Id1 when reverse-drag cluster).
        /// </summary>
        public int TargetProcessesCount(int lowId, int highId)
        {
            int[] ids = ExpandTradeSkillIds(lowId, highId);
            int count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                int id = ids[i];
                count += this.tradeSkillList.Count(x => x.ID2 == id || (x.IsImplant && x.ID1 == id));
            }

            int high = highId > 0 ? highId : lowId;
            return count
                   + ZoneEngine.Core.Arete.Quests.PersonalizedRobotBrainCombineRules.TargetProcessBonus(high)
                   + ZoneEngine.Core.Arete.Quests.VernonGodfrayCombineRules.TargetProcessBonus(high)
                   + ZoneEngine.Core.Arete.Quests.DoctorMasonCombineRules.TargetProcessBonus(high)
                   + ZoneEngine.Core.Arete.Quests.LoreleiCombineRules.TargetProcessBonus(high);
        }

        #endregion

        #region Methods

        private TradeSkillMatch TryResolveDbPair(
            int sourceLowId,
            int sourceHighId,
            int targetLowId,
            int targetHighId)
        {
            // Forward: UI Source = DB Id1, UI Target = DB Id2.
            int[] sourceIds = ExpandTradeSkillIds(sourceLowId, sourceHighId);
            int[] targetIds = ExpandTradeSkillIds(targetLowId, targetHighId);
            for (int s = 0; s < sourceIds.Length; s++)
            {
                for (int t = 0; t < targetIds.Length; t++)
                {
                    TradeSkillEntry entry = this.LookupPair(sourceIds[s], targetIds[t]);
                    if (entry != null)
                    {
                        return new TradeSkillMatch { Entry = entry, Swapped = false };
                    }
                }
            }

            // Reverse: UI Source = DB Id2 (implant), UI Target = DB Id1 (cluster).
            for (int s = 0; s < sourceIds.Length; s++)
            {
                for (int t = 0; t < targetIds.Length; t++)
                {
                    TradeSkillEntry entry = this.LookupPair(targetIds[t], sourceIds[s]);
                    if (entry != null)
                    {
                        return new TradeSkillMatch { Entry = entry, Swapped = true };
                    }
                }
            }

            return null;
        }

        private TradeSkillEntry LookupPair(int id1, int id2)
        {
            TradeSkillEntry entry;
            return this.tradeSkillByPair.TryGetValue(PairKey(id1, id2), out entry) ? entry : null;
        }

        /// <summary>
        /// Tradeskill Id1/Id2 are High AOIDs. Expand Low/High plus ItemLoader relation endpoints.
        /// </summary>
        private static int[] ExpandTradeSkillIds(int lowId, int highId)
        {
            var ids = new List<int>();
            AddId(ids, highId);
            AddId(ids, lowId);
            AddRelationEndpoints(ids, highId);
            AddRelationEndpoints(ids, lowId);
            return ids.ToArray();
        }

        private static void AddRelationEndpoints(List<int> ids, int startId)
        {
            if (startId <= 0 || ItemLoader.ItemList == null || !ItemLoader.ItemList.ContainsKey(startId))
            {
                return;
            }

            IEnumerable<int> relations = ItemLoader.ItemList[startId].Relations;
            if (relations == null)
            {
                return;
            }

            int minId = -1;
            int maxId = -1;
            foreach (int id in relations)
            {
                if (!ItemLoader.ItemList.ContainsKey(id))
                {
                    continue;
                }

                int q = ItemLoader.ItemList[id].Quality;
                if (minId < 0 || q < ItemLoader.ItemList[minId].Quality)
                {
                    minId = id;
                }

                if (maxId < 0 || q > ItemLoader.ItemList[maxId].Quality)
                {
                    maxId = id;
                }
            }

            // High AOID first — matches tradeskill Id1/Id2 convention.
            AddId(ids, maxId);
            AddId(ids, minId);
        }

        private static void AddId(List<int> ids, int id)
        {
            if (id > 0 && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        private static long PairKey(int id1, int id2)
        {
            return ((long)id1 << 32) | (uint)id2;
        }

        /// <summary>
        /// </summary>
        private void CacheItemNames()
        {
            foreach (DBItemName itemName in ItemNamesDao.Instance.GetAll())
            {
                this.ItemNames.Add(itemName.Id, itemName.Name);
            }
        }

        /// <summary>
        /// </summary>
        private void CacheTradeSkills()
        {
            int i = 0;
            int skipped = 0;
            this.tradeSkillList.Clear();
            this.tradeSkillByPair.Clear();
            foreach (DBTradeSkill tradeSkill in TradeSkillDao.Instance.GetAll())
            {
                try
                {
                    if (tradeSkill.Id1 == 0 && tradeSkill.Id2 == 0)
                    {
                        skipped++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(tradeSkill.ResultIds))
                    {
                        skipped++;
                        continue;
                    }

                    TradeSkillEntry entry = TradeSkillEntry.ConvertFromDB(tradeSkill);
                    this.tradeSkillList.Add(entry);
                    long key = PairKey(entry.ID1, entry.ID2);
                    if (!this.tradeSkillByPair.ContainsKey(key))
                    {
                        this.tradeSkillByPair.Add(key, entry);
                    }

                    i++;
                    if ((i % 1000) == 0)
                    {
                        Console.Write("\rCached {0} trade skill entries", i);
                    }
                }
                catch
                {
                    skipped++;
                }
            }

            if (skipped > 0)
            {
                Console.WriteLine("\rCached {0} trade skill entries ({1} skipped)", i, skipped);
            }
        }

        #endregion
    }
}
