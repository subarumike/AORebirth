namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    /// <summary>
    /// One trained perk in the FullCharacter perk map (Gamecode.dll 0x10053ac9 reads the X3F1 array,
    /// 0x10052b7d each entry): the key perk id, then a version marker 0xFFFFFF00 | version, the perk id
    /// and a value the client keeps only for research perks. Version 0 carries nothing further.
    /// </summary>
    public class PerkMapEntry
    {
        public const int VersionZero = unchecked((int)0xFFFFFF00);

        [AoMember(0)]
        public int Key { get; set; }

        [AoMember(1)]
        public int Marker { get; set; }

        [AoMember(2)]
        public int PerkId { get; set; }

        [AoMember(3)]
        public int Value { get; set; }
    }
}
