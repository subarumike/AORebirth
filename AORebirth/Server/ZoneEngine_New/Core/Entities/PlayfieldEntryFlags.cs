namespace ZoneEngine_New.Core.Entities
{
    using AORebirth.Core.GameData;

    /// <summary>
    /// The playfield facts the client derives on entering a playfield (Gamecode.dll FUN_1005975f) and that item
    /// criteria read: PlayfieldType (438), ExpansionPlayfield (531), PlayfieldProxy (589) and the FlyingAllowed
    /// operator (112, Gamecode.dll FUN_100065d8 case 0x70). Computed from the playfield's metadata.json.
    /// </summary>
    public static class PlayfieldEntryFlags
    {
        /// <summary>PlayfieldType bit 1: an indoor/dungeon tilemap (N3.dll n3Playfield_t::IsDungeon, tilemap type 1).</summary>
        public const int DungeonBit = 0x1;

        /// <summary>PlayfieldType bit 2: a battle station (ids 4380-4388, n3Playfield_t::IsBattleStation) or playfield 6104.</summary>
        public const int BattleStationBit = 0x2;

        /// <summary>
        /// Metadata unknownV9A bit the client reads as RDBPlayfield+0x50 bit 0x2 (PlayfieldAnarchy_t::IsShadowlandPF). Set
        /// on exactly the Shadowlands playfields. Mapped by correlating values; the field keeps its name until the RDB
        /// parser offset is confirmed.
        /// </summary>
        public const int ShadowlandsMetaDataBit = 0x2;

        /// <summary>
        /// Metadata playfieldTypeBits bit the client reads as RDBPlayfield+0x58 0x80000: flying is not allowed (the
        /// Shadowlands, the Alien Mothership, battle stations, ICC Shuttleport). Rubi-Ka cities use 0x1000000 instead.
        /// </summary>
        public const int NoFlyingMetaDataBit = 0x80000;

        /// <summary>
        /// PlayfieldType for <paramref name="playfieldId"/>. Without metadata the playfield counts as indoor, as the rest of
        /// the zone engine treats it.
        /// </summary>
        public static int PlayfieldType(int playfieldId, PlayfieldMetaData? metaData)
        {
            bool dungeon = metaData?.IsIndoor ?? true;
            bool battleStation = playfieldId is >= 4380 and <= 4388 || playfieldId == 6104;
            return (dungeon ? DungeonBit : 0) | (battleStation ? BattleStationBit : 0);
        }

        /// <summary>ExpansionPlayfield: 1 in the Shadowlands, else 0.</summary>
        public static int ExpansionPlayfield(PlayfieldMetaData? metaData)
            => metaData != null && (metaData.UnknownV9A & ShadowlandsMetaDataBit) != 0 ? 1 : 0;

        /// <summary>
        /// FlyingAllowed: outdoors, outside the Shadowlands, on a playfield whose metadata does not forbid flying. The
        /// client's own check reads only the no-fly bit, which most indoor playfields (the Grid 152 among them) leave
        /// clear; flying indoors or in the Shadowlands is a server rule. No metadata fails closed.
        /// </summary>
        public static bool IsFlyingAllowed(PlayfieldMetaData? metaData)
            => metaData != null
               && !metaData.IsIndoor
               && ExpansionPlayfield(metaData) == 0
               && (metaData.PlayfieldTypeBits & NoFlyingMetaDataBit) == 0;
    }
}
