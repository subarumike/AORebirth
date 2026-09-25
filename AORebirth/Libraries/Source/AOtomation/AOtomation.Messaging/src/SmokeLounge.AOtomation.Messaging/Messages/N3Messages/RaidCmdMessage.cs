namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// Capture 20260902-073932: OUT RaidCmd Command=1 → Convert team to raid.
    /// Capture 20260924-213512: OUT RaidCmd Command=4 → Move raid member
    /// (TargetCharacterId + DestinationTeamIndex), then IN TeamMemberLeft + TeamMember
    /// with Unknown4 = DestinationTeamIndex.
    /// </summary>
    [AoContract((int)N3MessageType.RaidCmd)]
    public class RaidCmdMessage : N3Message
    {
        public RaidCmdMessage()
        {
            this.N3MessageType = N3MessageType.RaidCmd;
        }

        /// <summary>1 = convert to raid; 4 = move member between raid teams.</summary>
        [AoMember(0)]
        public int Command { get; set; }

        /// <summary>Command=4: character instance to move. Command=1: 0.</summary>
        [AoMember(1)]
        public int TargetCharacterId { get; set; }

        /// <summary>Command=4: destination raid team index (emitted as TeamMember.Unknown4). Command=1: 0.</summary>
        [AoMember(2)]
        public int DestinationTeamIndex { get; set; }
    }
}
