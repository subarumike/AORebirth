using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class FollowCoordinateInfo : FollowInfo
    {
        private byte followInfoType = 1;

        public FollowCoordinateInfo()
        {
            this.Coordinates = new List<Vector3>();
        }

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
        [AoMember(1)]
        public byte MoveMode { get; set; }

        [AoMember(2)]
        public byte CoordinateCount { get; set; }

        [AoMember(3)]
        public Vector3 CurrentCoordinates { get; set; }

        [AoMember(4)]
        public Vector3 EndCoordinates { get; set; }

        public List<Vector3> Coordinates { get; set; }
    }
}
