namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// Capture 20260902-073932 / Pandemonium 20260902-071644: server→client raid convert ack.
    /// Wire: N3 header (Identity=self, Unknown=0) + Int16 0.
    /// </summary>
    [AoContract((int)N3MessageType.Raid)]
    public class RaidMessage : N3Message
    {
        public RaidMessage()
        {
            this.N3MessageType = N3MessageType.Raid;
        }

        /// <summary>Capture tail Int16=0 after Unknown byte.</summary>
        [AoMember(0)]
        public short Unknown1 { get; set; }
    }
}
