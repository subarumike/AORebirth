using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class TowerProxyBase
    {
        [AoMember(1)]
        public Identity TowerFieldIdentity { get; set; }
        [AoMember(2)]
        public Identity OwnerIdentity { get; set; }
        [AoMember(3)]
        public Vector3 Coordinates { get; set; }
        [AoMember(4)]
        public int Unknown1 { get; set; }
        [AoMember(5)]
        public int Unknown2 { get; set; }
        [AoMember(6)]
        public int Unknown3 { get; set; }
        [AoMember(7)]
        public Single Unknown4 { get; set; }
        [AoMember(8)]
        public int Unknown5 { get; set; }
    }
}
