namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class MissionItemReward
    {
        [AoMember(0)]
        public int LowId { get; set; }

        [AoMember(1)]
        public int HighId { get; set; }

        [AoMember(2)]
        public int Ql { get; set; }

        [AoMember(3)]
        public int Unknown { get; set; }
    }
}
