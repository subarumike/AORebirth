namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// Capture 20260902-073932: OUT RaidCmd Command=1 → Convert team to raid.
    /// </summary>
    [AoContract((int)N3MessageType.RaidCmd)]
    public class RaidCmdMessage : N3Message
    {
        public RaidCmdMessage()
        {
            this.N3MessageType = N3MessageType.RaidCmd;
        }

        /// <summary>1 = convert current team to raid (capture 20260902-073932).</summary>
        [AoMember(0)]
        public int Command { get; set; }
    }
}
