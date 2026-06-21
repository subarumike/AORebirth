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

namespace AORebirth.Core.Inventory
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// </summary>
    public abstract class BaseInventoryPages : IInventoryPages
    {
        private readonly IDictionary<int, ulong> backpackHandlePages;

        private readonly IDictionary<ulong, IInventoryPage> backpackPages;

        private readonly ISet<ulong> openBackpackPages;

        private bool disposed = false;

        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        /// <param name="standardpage">
        /// </param>
        /// <param name="owner">
        /// </param>
        public BaseInventoryPages(int standardpage, IItemContainer owner)
            : this()
        {
            this.StandardPage = standardpage;
            this.Owner = owner;
        }

        /// <summary>
        /// </summary>
        private BaseInventoryPages()
        {
            this.Pages = new Dictionary<int, IInventoryPage>();
            this.backpackHandlePages = new Dictionary<int, ulong>();
            this.backpackPages = new Dictionary<ulong, IInventoryPage>();
            this.openBackpackPages = new HashSet<ulong>();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// </summary>
        public IItemContainer Owner { get; private set; }

        /// <summary>
        /// </summary>
        public IDictionary<int, IInventoryPage> Pages { get; private set; }

        /// <summary>
        /// </summary>
        public int StandardPage { get; set; }

        #endregion

        #region Public Indexers

        /// <summary>
        /// </summary>
        /// <param name="index">
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// </exception>
        /// <returns>
        /// </returns>
        public IInventoryPage this[int index]
        {
            get
            {
                if (!this.Pages.ContainsKey(index))
                {
                    throw new ArgumentOutOfRangeException("There is no inventorypage #" + index);
                }

                return this.Pages[index];
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="pageNum">
        /// </param>
        /// <param name="slotNum">
        /// </param>
        /// <param name="item">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// </exception>
        public InventoryError AddToPage(int pageNum, int slotNum, IItem item)
        {
            if (!this.Pages.ContainsKey(pageNum))
            {
                throw new ArgumentOutOfRangeException("There is no inventorypage #" + pageNum);
            }

            if (this.HasUniqueItemAlready(item))
            {
                return InventoryError.HaveUniqueAlready;
            }

            return this.Pages[pageNum].Add(slotNum, item);
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        public void CalculateModifiers(Character character)
        {
            foreach (IInventoryPage page in this.Pages.Values)
            {
                page.CalculateModifiers(character);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="targetLocation">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NullReferenceException">
        /// </exception>
        public Item GetItemAt(int targetLocation)
        {
            // TODO: Maybe check not only for BankInventoryPage here (TradeWindow, knubot etc)
            foreach (BaseInventoryPage inventoryPage in this.Pages.Values.Where(x => !(x is BankInventoryPage)))
            {
                if (inventoryPage.ValidSlot(targetLocation))
                {
                    if (inventoryPage[targetLocation] != null)
                    {
                        return (Item)inventoryPage[targetLocation];
                    }
                }
            }

            throw new NullReferenceException("Inventory mismatch!");
        }

        /// <summary>
        /// </summary>
        /// <param name="container">
        /// </param>
        /// <param name="placement">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NullReferenceException">
        /// </exception>
        public Item GetItemInContainer(int container, int placement)
        {
            try
            {
                IInventoryPage inventoryPage = this.Pages[container];
                if (inventoryPage.ValidSlot(placement))
                {
                    if (inventoryPage[placement] != null)
                    {
                        return (Item)inventoryPage[placement];
                    }
                }

                throw new NullReferenceException("Container/Placement error: " + container + "/" + placement);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="containerIdentity">
        /// </param>
        /// <returns>
        /// </returns>
        public IInventoryPage GetOrCreateBackpackPage(Identity containerIdentity)
        {
            if ((containerIdentity == null) || (containerIdentity.Type != IdentityType.Container))
            {
                throw new ArgumentException("Backpack page identity must be a Container identity.", "containerIdentity");
            }

            IInventoryPage page;
            if (this.TryGetBackpackPage(containerIdentity, out page))
            {
                return page;
            }

            if (this.Owner == null)
            {
                throw new InvalidOperationException("Inventory page owner is required for backpack pages.");
            }

            page = new BackPackInventoryPage(this.Owner.Identity, containerIdentity);
            page.Read();
            this.backpackPages.Add(containerIdentity.Long(), page);
            return page;
        }

        /// <summary>
        /// </summary>
        /// <param name="containerIdentity">
        /// </param>
        /// <param name="page">
        /// </param>
        /// <returns>
        /// </returns>
        public bool TryGetBackpackPage(Identity containerIdentity, out IInventoryPage page)
        {
            page = null;
            if (containerIdentity == null)
            {
                return false;
            }

            return this.backpackPages.TryGetValue(containerIdentity.Long(), out page);
        }

        public bool IsBackpackOpen(Identity containerIdentity)
        {
            if ((containerIdentity == null) || (containerIdentity.Type != IdentityType.Container))
            {
                return false;
            }

            return this.openBackpackPages.Contains(containerIdentity.Long());
        }

        public void MarkBackpackOpen(Identity containerIdentity)
        {
            if ((containerIdentity == null) || (containerIdentity.Type != IdentityType.Container))
            {
                return;
            }

            this.openBackpackPages.Add(containerIdentity.Long());
        }

        public void MarkBackpackClosed(Identity containerIdentity)
        {
            if ((containerIdentity == null) || (containerIdentity.Type != IdentityType.Container))
            {
                return;
            }

            this.openBackpackPages.Remove(containerIdentity.Long());
        }

        /// <summary>
        /// </summary>
        /// <param name="handle">
        /// </param>
        /// <param name="containerIdentity">
        /// </param>
        public void RegisterBackpackHandle(int handle, Identity containerIdentity)
        {
            if ((containerIdentity == null) || (containerIdentity.Type != IdentityType.Container))
            {
                throw new ArgumentException("Backpack handle must map to a Container identity.", "containerIdentity");
            }

            this.backpackHandlePages[handle] = containerIdentity.Long();
        }

        /// <summary>
        /// </summary>
        /// <param name="handle">
        /// </param>
        /// <param name="page">
        /// </param>
        /// <returns>
        /// </returns>
        public bool TryGetBackpackPageByHandle(int handle, out IInventoryPage page)
        {
            page = null;

            ulong containerKey;
            if (!this.backpackHandlePages.TryGetValue(handle, out containerKey))
            {
                return false;
            }

            return this.backpackPages.TryGetValue(containerKey, out page);
        }

        /// <summary>
        /// </summary>
        /// <param name="slotNum">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public IInventoryPage PageFromSlot(int slotNum)
        {
            foreach (KeyValuePair<int, IInventoryPage> page in this.Pages)
            {
                if ((slotNum >= page.Value.FirstSlotNumber)
                    && (slotNum <= page.Value.FirstSlotNumber + page.Value.MaxSlots))
                {
                    return page.Value;
                }
            }

            if (slotNum == (int)IdentityType.TradeWindow)
            {
                return this.Pages[this.StandardPage];
            }

            throw new IndexOutOfRangeException("No inventory page found for slot " + slotNum);
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public bool Read()
        {
            foreach (IInventoryPage inventoryPage in this.Pages.Values)
            {
                inventoryPage.Read();
            }

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="pageNum">
        /// </param>
        /// <param name="slotNum">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// </exception>
        public IItem RemoveItem(int pageNum, int slotNum)
        {
            if (!this.Pages.ContainsKey(pageNum))
            {
                throw new ArgumentOutOfRangeException("There is no inventorypage #" + pageNum);
            }

            return this.Pages[pageNum].Remove(slotNum);
        }

        /// <summary>
        /// </summary>
        /// <param name="statId">
        /// </param>
        /// <returns>
        /// </returns>
        public int Stat(int statId)
        {
            // TODO: Can be optimized, hardcoding the page numbers to be considered equipmentpages

            int value = 0;
            foreach (IInventoryPage inventoryPage in this.Pages.Values)
            {
                if (inventoryPage is IEquipmentPage)
                {
                    value += ((IEquipmentPage)inventoryPage).Stat(statId);
                }
            }

            return value;
        }

        /// <summary>
        /// </summary>
        /// <param name="item">
        /// </param>
        /// <returns>
        /// </returns>
        public InventoryError TryAdd(IItem item)
        {
            try
            {
                int freeSlot = this.Pages[this.StandardPage].FindFreeSlot();
                if (freeSlot == -1)
                {
                    return InventoryError.InventoryIsFull;
                }

                return this.AddToPage(this.StandardPage, freeSlot, item);
            }
            catch
            {
                return InventoryError.InventoryIsFull;
            }

        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public bool Write()
        {
            foreach (IInventoryPage inventoryPage in this.Pages.Values)
            {
                inventoryPage.Write();
            }

            foreach (IInventoryPage inventoryPage in this.backpackPages.Values)
            {
                inventoryPage.Write();
            }

            return true;
        }

        #endregion

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.disposed)
                {
                    foreach (KeyValuePair<int, IInventoryPage> kv in this.Pages)
                    {
                        ((PooledObject)kv.Value).Dispose();
                    }
                    this.Pages.Clear();

                    foreach (KeyValuePair<ulong, IInventoryPage> kv in this.backpackPages)
                    {
                        ((PooledObject)kv.Value).Dispose();
                    }
                    this.backpackPages.Clear();
                    this.backpackHandlePages.Clear();
                }
            }
            this.disposed = true;
        }

        private bool HasUniqueItemAlready(IItem item)
        {
            return InventoryItemRules.HasSameUniqueItem(
                item,
                this.Pages.Values.SelectMany(page => page.List()).Select(existing => existing.Value));
        }
    }
}
