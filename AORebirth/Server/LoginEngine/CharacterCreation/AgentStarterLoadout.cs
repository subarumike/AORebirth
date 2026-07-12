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

namespace LoginEngine.CharacterCreation
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Level-1 Agent starter inventory from capture 20260711-agent-starter-kit (FullCharacter slots 64-71).
    /// </summary>
    internal static class AgentStarterLoadout
    {
        private struct StarterItemDefinition
        {
            public int Placement;
            public int LowId;
            public int HighId;
            public int Quality;
            public int Count;
        }

        private static readonly StarterItemDefinition[] Items =
        {
            new StarterItemDefinition { Placement = 64, LowId = 291082, HighId = 291082, Quality = 1, Count = 50 },
            new StarterItemDefinition { Placement = 65, LowId = 291043, HighId = 291043, Quality = 1, Count = 25 },
            new StarterItemDefinition { Placement = 66, LowId = 252158, HighId = 252158, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 67, LowId = 292235, HighId = 292235, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 68, LowId = 296977, HighId = 296977, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 69, LowId = 287047, HighId = 287047, Quality = 25, Count = 1 },
            new StarterItemDefinition { Placement = 70, LowId = 121568, HighId = 121568, Quality = 1, Count = 1 },
            new StarterItemDefinition { Placement = 71, LowId = 56238, HighId = 56238, Quality = 1, Count = 1 },
        };

        public static void Apply(int characterId)
        {
            ApplyStarterItems(characterId);
        }

        private static void ApplyStarterItems(int characterId)
        {
            int containerType = characterId;
            int containerInstance = (int)IdentityType.Inventory;
            var items = new List<DBItem>(Items.Length);

            foreach (StarterItemDefinition entry in Items)
            {
                items.Add(
                    new DBItem
                    {
                        containertype = containerType,
                        containerinstance = containerInstance,
                        containerplacement = entry.Placement,
                        lowid = entry.LowId,
                        highid = entry.HighId,
                        quality = entry.Quality,
                        multiplecount = entry.Count
                    });
            }

            ItemDao.Instance.Save(items, null, null);
        }
    }
}
