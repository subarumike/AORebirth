namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages.OrgServerMessages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// Presidency transfer confirmation dialog (capture 20261031). On the wire the two base integers (Unknown1, Unknown2)
    /// are the new leader's identity (type, instance) and Organization is the organization; the name stays empty.
    /// </summary>
    [AoContract((byte)OrgServerMessageType.OrgLeadershipTransfer)]
    public class OrgLeadershipTransferMessage : OrgServerMessage
    {
        public OrgLeadershipTransferMessage()
        {
            this.OrgServerMessageType = OrgServerMessageType.OrgLeadershipTransfer;
            this.OrganizationName = string.Empty;
        }
    }
}
