namespace SmokeLounge.AOtomation.Messaging.GameData
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    #endregion

    /// <summary>
    /// </summary>
    public class FollowTargetInfo : FollowInfo
    {
        private byte followInfoType = 2;

        /// <summary>
        /// </summary>
        [AoMember(0)]
        public byte FollowInfoType
        {
            get
            {
                return this.followInfoType;
            }
            set
            {
                this.followInfoType = value;
            }
        }

        public FollowTargetInfo()
        {
            this.MoveType = 0;
        }

        /// <summary>
        /// DataLength is 0 for FollowTargetInfo
        /// </summary>
        [AoMember(1)]
        public byte MoveType { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(2)]
        public Identity Target { get; set; }

        [AoMember(3)]

        public byte Dummy { get; set; }
        /// <summary>
        /// 0x20000000
        /// </summary>
        [AoMember(4)]
        public int Dummy1 { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(5)]
        public float X { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(6)]
        public float Y { get; set; }

        /// <summary>
        /// </summary>
        [AoMember(7)]
        public float Z { get; set; }
    }
}