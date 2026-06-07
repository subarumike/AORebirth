namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    #endregion

    /// <summary>
    /// </summary>
    [AoContract((int)N3MessageType.InventoryUpdate)]
    public class InventoryUpdateMessage : N3Message
    {
        public InventoryUpdateMessage()
        {
            this.N3MessageType = N3MessageType.InventoryUpdate;
        }

        /// <summary>
        /// </summary>
        [AoMember(0)]
        public int NumberOfSlots { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(1)]
        // 0x00000003
        public int Unknown1 { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(2, SerializeSize = ArraySizeType.X3F1)]
        public InventoryEntry[] Entries { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(3)]
        public Identity BagIdentity { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(4)]
        public int SlotnumberInMainInventory { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(5)]
        // 0x00000001
        public int Unknown2 { get; set; }
    }
}