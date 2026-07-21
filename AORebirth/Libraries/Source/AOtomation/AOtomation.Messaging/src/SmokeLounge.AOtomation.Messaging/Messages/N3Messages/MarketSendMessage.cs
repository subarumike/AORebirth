// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MarketSendMessage.cs" company="AORebirth">
/* Capture: 20260715-GMI
 * Type 0x470B2E14. Mail-style Type+Identity+Unknown(byte)+Character Identity, then:
 *   credit deposit: credits + 0x3F1
 *   item deposit: credits=0, then one or more (itemLowId, containerType Inventory=0x68, placement)
 * Deposit UI has 8 slots — wire may carry up to 8 triples in one Send (or one packet per item).
 * Live DeleteItem used Inventory + placement matching the third int. */
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    [AoContract((int)N3MessageType.MarketSend)]
    public class MarketSendMessage : N3Message
    {
        public const int CaptureCreditStatusCode = 0x3F1;

        public const int MaxDepositItems = 8;

        public MarketSendMessage()
        {
            this.N3MessageType = N3MessageType.MarketSend;
            this.Unknown = 0;
            this.Character = new Identity();
            this.Items = new List<MarketSendItemEntry>();
        }

        public Identity Character { get; set; }

        public int Credits { get; set; }

        /// <summary>Item low template id on item deposit (first entry; mirrors Items[0]).</summary>
        public int ItemLowId { get; set; }

        /// <summary>Container identity type (capture Inventory = 0x68 / 104).</summary>
        public int ContainerType { get; set; }

        /// <summary>Absolute inventory slot / placement.</summary>
        public int Placement { get; set; }

        /// <summary>0x3F1 on credit deposit / ack.</summary>
        public int StatusCode { get; set; }

        /// <summary>All item slots from one Deposit Send (max 8).</summary>
        public List<MarketSendItemEntry> Items { get; set; }

        public bool IsItemDeposit
        {
            get
            {
                if (this.Items != null && this.Items.Count > 0)
                {
                    return true;
                }

                return this.ItemLowId != 0 && this.ContainerType != 0;
            }
        }

        public bool IsCreditDeposit
        {
            get { return this.Credits > 0; }
        }

        public void SyncFirstItemFields()
        {
            if (this.Items == null || this.Items.Count == 0)
            {
                return;
            }

            MarketSendItemEntry first = this.Items[0];
            this.ItemLowId = first.ItemLowId;
            this.ContainerType = first.ContainerType;
            this.Placement = first.Placement;
        }
    }

    public class MarketSendItemEntry
    {
        public int ItemLowId { get; set; }

        public int ContainerType { get; set; }

        public int Placement { get; set; }
    }
}
