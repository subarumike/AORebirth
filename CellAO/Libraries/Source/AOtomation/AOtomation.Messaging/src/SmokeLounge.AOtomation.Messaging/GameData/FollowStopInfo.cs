namespace SmokeLounge.AOtomation.Messaging.GameData
{
    public class FollowStopInfo : FollowInfo
    {
        private byte followInfoType = 2;

        public FollowStopInfo()
        {
            this.MoveType = 21;
            this.Flag = 1;
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

        public byte Flag { get; set; }

        public Vector3 ConfirmCoordinates { get; set; }
    }
}
