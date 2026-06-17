namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class CharacterInfo
    {
        [AoMember(0)]
        public Identity MissionIdentity { get; set; }

        [AoMember(1, SerializeSize = ArraySizeType.NullTerminated)]
        public string Name { get; set; }
    }
}
