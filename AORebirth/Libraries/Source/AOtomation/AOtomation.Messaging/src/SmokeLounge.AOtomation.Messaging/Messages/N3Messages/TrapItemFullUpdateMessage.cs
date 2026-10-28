namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>Recovered common FullUpdate prefix and TrapItem fields; neutral values retain their offset names.</summary>
    [AoContract((int)N3MessageType.TrapItemFullUpdate)]
    public class TrapItemFullUpdateMessage : N3Message
    {
        public TrapItemFullUpdateMessage()
        {
            this.N3MessageType = N3MessageType.TrapItemFullUpdate;
            this.CommonMarker = 11;
            this.TrapMarker = 2;
            this.Stats = new GameTuple<CharacterStat, uint>[0];
            this.OpaqueData = new byte[0];
        }

        [AoMember(0)]
        public int CommonMarker { get; set; }

        public Identity Parent { get; set; }

        [AoMember(1)]
        [AoFlags("trapParentType")]
        public int ParentType
        {
            get { return (int)this.Parent.Type; }
            set { this.Parent = new Identity { Type = (IdentityType)value, Instance = this.Parent.Instance }; }
        }

        [AoMember(2)]
        public int ParentInstance
        {
            get { return this.Parent.Instance; }
            set { this.Parent = new Identity { Type = this.Parent.Type, Instance = value }; }
        }

        // Gamecode+A1661: parented updates contain no absolute transform.
        [AoMember(3)]
        [AoUsesFlags("trapParentType", typeof(Vector3), FlagsCriteria.EqualsToAny, new[] { 0 })]
        public Vector3 Coordinate { get; set; }

        [AoMember(4)]
        [AoUsesFlags("trapParentType", typeof(Quaternion), FlagsCriteria.EqualsToAny, new[] { 0 })]
        public Quaternion Heading { get; set; }

        [AoMember(5)]
        public int PlayfieldId { get; set; }

        [AoMember(6)]
        public Identity NeutralIdentity { get; set; }

        [AoMember(7)]
        public short HeaderStatBytes { get; set; }

        [AoMember(8, SerializeSize = ArraySizeType.X3F1)]
        public GameTuple<CharacterStat, uint>[] Stats { get; set; }

        [AoMember(9, SerializeSize = ArraySizeType.Int32)]
        public byte[] OpaqueData { get; set; }

        // Gamecode+A28CA: marker2, raw+74, neutral identities+78/+80, flags.
        [AoMember(10)]
        public int TrapMarker { get; set; }

        [AoMember(11)]
        public int Raw74 { get; set; }

        [AoMember(12)]
        public Identity Identity78 { get; set; }

        [AoMember(13)]
        public Identity Identity80 { get; set; }

        [AoMember(14)]
        public byte TrapFlags { get; set; }
    }
}
