namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    #endregion

    /// <summary>
    /// Yes/No dialog sent to online members when the organization tax rises. Captured as OrgServer sub-type 0x08:
    /// Unknown1, Unknown2, Organization identity (0x0000DEAA), then the new tax as the last int. Unlike the other
    /// OrgServer sub-types there is no organization name field, so this message does not derive from OrgServerMessage.
    /// No AoContract: the OrgServer identifier is already registered by OrgServerMessage, and a duplicate
    /// registration throws at startup.
    /// </summary>
    public class OrgTaxIncreaseMessage : N3Message
    {
        public OrgTaxIncreaseMessage()
        {
            this.N3MessageType = N3MessageType.OrgServer;
            this.OrgServerMessageType = OrgServerMessageType.OrgTaxIncrease;
        }

        [AoMember(0)]
        public OrgServerMessageType OrgServerMessageType { get; set; }

        [AoMember(1)]
        public int Unknown1 { get; set; }

        [AoMember(2)]
        public int Unknown2 { get; set; }

        [AoMember(3)]
        public Identity Organization { get; set; }

        [AoMember(4)]
        public int NewTax { get; set; }
    }
}
