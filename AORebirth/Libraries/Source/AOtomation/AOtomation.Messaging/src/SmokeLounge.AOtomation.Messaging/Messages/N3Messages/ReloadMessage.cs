namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// Server→client weapon reload confirmation. Capture 20260728-221109 / live Reload IIR fixed 12.
    /// Body after identity: status int + ammo inventory identity.
    /// </summary>
    [AoContract((int)N3MessageType.Reload)]
    public class ReloadMessage : N3Message
    {
        public ReloadMessage()
        {
            this.N3MessageType = N3MessageType.Reload;
        }

        /// <summary>Capture gold uses 1 on successful reload.</summary>
        [AoMember(0)]
        public int Status { get; set; }

        /// <summary>Ammo stack identity (Inventory page + instance) consumed for the reload.</summary>
        [AoMember(1)]
        public Identity AmmoIdentity { get; set; }
    }
}
