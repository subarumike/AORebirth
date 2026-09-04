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

namespace AORebirth.Core.Entities
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    #endregion

    public class TemporaryBag : PooledObject
    {
        public static IdentityType Type = IdentityType.TempBag;

        private readonly OutgoingTradeInventoryPage charactersBag;

        private readonly KnuBotTradeInventoryPage vendorsBag;

        private readonly IncomingTradeInventoryPage playerVendorBag;

        private readonly HashSet<int> acceptedPlayerTradeCharacters = new HashSet<int>();

        private readonly HashSet<int> endedPlayerTradeCharacters = new HashSet<int>();

        private readonly Dictionary<int, int> playerTradeCredits = new Dictionary<int, int>();

        private bool playerTradeCompletionStarted;

        public TemporaryBag(Identity parent, Identity id, Identity shopper, Identity vendor, int vendorSlots = 255)
            : base(parent, id)
        {
            this.Shopper = shopper;
            this.Vendor = vendor;
            this.charactersBag = new OutgoingTradeInventoryPage(id, vendorSlots);
            this.vendorsBag = new KnuBotTradeInventoryPage(id);
            this.playerVendorBag = new IncomingTradeInventoryPage(id);
        }

        public Identity Shopper { get; set; }

        public Identity Vendor { get; set; }

        public bool Add(Identity from, IItem item)
        {
            if (from.Equals(this.Shopper))
            {
                this.vendorsBag.Add(this.vendorsBag.FindFreeSlot(), item);
                LogUtil.Debug(DebugInfoDetail.Shopping, "Added Item from character " + from.ToString(true));
            }
            else
            {
                this.charactersBag.Add(from.Instance);
                LogUtil.Debug(DebugInfoDetail.Shopping, "Added Item from shop on position " + from.ToString(true));
            }

            // For now no invalid trades
            return true;
        }

        public int AddPlayerOffer(Identity from, IItem item)
        {
            IInventoryPage offerPage = this.GetPlayerOfferPage(from);
            int slot = offerPage.FindFreeSlot();
            if (slot >= 0)
            {
                offerPage.Add(slot, item);
                this.ClearPlayerTradeAcceptances();
            }

            return slot;
        }

        public IItem Remove(Identity from, int slot)
        {
            if (from.Equals(this.Shopper))
            {
                LogUtil.Debug(DebugInfoDetail.Shopping, "Removed Item from character in shopbag from slot " + slot);
                return this.vendorsBag.Remove(slot);
            }
            this.charactersBag.Remove(slot);
            return null;
        }

        public IItem RemovePlayerOffer(Identity from, int slot)
        {
            IInventoryPage offerPage = this.GetPlayerOfferPage(from);
            IItem item = offerPage[slot];
            if (item == null)
            {
                return null;
            }

            offerPage.Remove(slot);
            this.ClearPlayerTradeAcceptances();
            return item;
        }

        public IInventoryPage GetPlayerOfferPage(Identity from)
        {
            if (from.Equals(this.Shopper))
            {
                return this.vendorsBag;
            }

            if (from.Equals(this.Vendor))
            {
                return this.playerVendorBag;
            }

            return null;
        }

        public IItem[] GetPlayerOffers(Identity from)
        {
            IInventoryPage offerPage = this.GetPlayerOfferPage(from);
            if (offerPage == null)
            {
                return new IItem[0];
            }

            return offerPage.List().Values.ToArray();
        }

        public void SetPlayerTradeCredits(Identity from, int credits)
        {
            this.playerTradeCredits[from.Instance] = credits < 0 ? 0 : credits;
            this.ClearPlayerTradeAcceptances();
        }

        public int GetPlayerTradeCredits(Identity from)
        {
            int credits;
            return this.playerTradeCredits.TryGetValue(from.Instance, out credits) ? credits : 0;
        }

        public bool MarkPlayerTradeAccepted(Identity identity)
        {
            this.acceptedPlayerTradeCharacters.Add(identity.Instance);
            return this.IsPlayerTradeReady();
        }

        public void MarkPlayerTradeEnded(Identity identity)
        {
            this.endedPlayerTradeCharacters.Add(identity.Instance);
        }

        public bool HasPlayerTradeEndRequest()
        {
            return this.endedPlayerTradeCharacters.Count > 0;
        }

        public bool IsPlayerTradeEnded()
        {
            return this.endedPlayerTradeCharacters.Contains(this.Shopper.Instance)
                   && this.endedPlayerTradeCharacters.Contains(this.Vendor.Instance);
        }

        public bool IsPlayerTradeReady()
        {
            return this.acceptedPlayerTradeCharacters.Contains(this.Shopper.Instance)
                   && this.acceptedPlayerTradeCharacters.Contains(this.Vendor.Instance);
        }

        public bool TryBeginPlayerTradeCompletion()
        {
            if (this.playerTradeCompletionStarted)
            {
                return false;
            }

            this.playerTradeCompletionStarted = true;
            return true;
        }

        public void ClearPlayerTradeAcceptances()
        {
            this.acceptedPlayerTradeCharacters.Clear();
            this.endedPlayerTradeCharacters.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Remove linkage from character object first
                this.ClearCharacterShoppingBag(this.Shopper);
                this.ClearCharacterShoppingBag(this.Vendor);

                // Dispose the internal inventory pages
                this.charactersBag.Dispose();
                this.vendorsBag.Dispose();
                this.playerVendorBag.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ClearCharacterShoppingBag(Identity identity)
        {
            ICharacter character = Pool.Instance.GetObject<ICharacter>(identity);
            if (character != null)
            {
                if (character.ShoppingBag == this)
                {
                    character.ShoppingBag = null;
                }
            }
        }

        public IItem[] GetBoughtItems()
        {
            IItemContainer seller = Pool.Instance.GetObject<IItemContainer>(this.Vendor);
            return charactersBag.GetItems(seller.BaseInventory[seller.BaseInventory.StandardPage]);
        }

        public IItem[] GetSoldItems()
        {
            return vendorsBag.List().Values.ToArray();
        }
    }
}
