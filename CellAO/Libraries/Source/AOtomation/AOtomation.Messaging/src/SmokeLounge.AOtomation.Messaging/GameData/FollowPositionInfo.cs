namespace SmokeLounge.AOtomation.Messaging.GameData
{
    public class FollowPositionInfo : FollowInfo
    {
        private byte followInfoType = 2;

        public FollowPositionInfo()
        {
            this.MoveType = 25;
            this.Unknown2 = 0x40;
        }

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

        public byte MoveType { get; set; }

        public int Unknown1 { get; set; }

        public int Unknown2 { get; set; }

        public int Unknown3 { get; set; }

        public Vector3 Coordinates { get; set; }

        public byte Unknown4 { get; set; }
    }
}
