// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CharacterInfoPacket.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the CharacterInfoPacket type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.GameData
{
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;
    using SmokeLounge.AOtomation.Messaging.Serialization.MappingAttributes;

    public class CharacterInfoPacket : InfoPacket
    {
        #region AoMember Properties

        [AoMember(0)]
        public byte Unknown1 { get; set; }

        [AoMember(1)]
        [AoUsesFlags("flags", typeof(byte), FlagsCriteria.Default)]
        public Profession Profession { get; set; }

        [AoMember(2)]
        public byte Level { get; set; }

        [AoMember(3)]
        public byte TitleLevel { get; set; }

        [AoMember(4)]
        [AoUsesFlags("flags", typeof(byte), FlagsCriteria.Default)]
        public Profession VisualProfession { get; set; }

        [AoMember(5)]
        public short SideXp { get; set; }

        [AoMember(6)]
        public int Health { get; set; }

        [AoMember(7)]
        public int MaxHealth { get; set; }

        [AoMember(8)]
        public int BreedHostility { get; set; }

        [AoMember(9)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasOrgRank)]
        public int? OrganizationId { get; set; }

        [AoMember(10, SerializeSize = ArraySizeType.Int16)]
        public string FirstName { get; set; }

        [AoMember(11, SerializeSize = ArraySizeType.Int16)]
        public string LastName { get; set; }

        [AoMember(12, SerializeSize = ArraySizeType.Int16)]
        public string LegacyTitle { get; set; }

        [AoMember(13, SerializeSize = ArraySizeType.Int16)]
        public string PvpTitle { get; set; }

        [AoMember(14, SerializeSize = ArraySizeType.Int16)]
        [AoUsesFlags("flags", typeof(string), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasOrgRank)]
        public string OrganizationRank { get; set; }

        [AoMember(15, SerializeSize = ArraySizeType.X3F1)]
        [AoUsesFlags("flags", typeof(TowerField[]), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasTowerFields)]
        public TowerField[] TowerFields { get; set; }

        [AoMember(16)]
        public int CityPlayfieldId { get; set; }

        [AoMember(17, SerializeSize = ArraySizeType.X3F1)]
        [AoUsesFlags("flags", typeof(Tower[]), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasTowers)]
        public Tower[] Towers { get; set; }

        [AoMember(18)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasGasChangeInfo)]
        public int? GasChangeTimer { get; set; }

        [AoMember(19)]
        [AoUsesFlags("flags", typeof(byte), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasGasChangeInfo)]
        public byte? GasLevel { get; set; }

        [AoMember(20)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? ClanSentinels { get; set; }

        [AoMember(21)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? OTMed { get; set; }

        [AoMember(22)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? ClanGaia { get; set; }

        [AoMember(23)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? OTTrans { get; set; }

        [AoMember(24)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? ClanVanguards { get; set; }

        [AoMember(25)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? GOS { get; set; }

        [AoMember(26)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? OTFollowers { get; set; }

        [AoMember(27)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? OTOperator { get; set; }

        [AoMember(28)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? OTUnredeemed { get; set; }

        [AoMember(29)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? ClanDevoted { get; set; }

        [AoMember(30)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? ClanConserver { get; set; }

        [AoMember(31)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasAll, (int)InfoPacketFlags.HasFactionInfo)]
        public int? ClanRedeemed { get; set; }

        [AoMember(32)]
        public int InvadersKilled { get; set; }

        [AoMember(33)]
        public int KilledByInvaders { get; set; }

        [AoMember(34)]
        public int AiLevel { get; set; }

        [AoMember(35)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasNone, (int)InfoPacketFlags.HasPvpInfoHidden)]
        public int? PvpDuelWins { get; set; }

        [AoMember(36)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasNone, (int)InfoPacketFlags.HasPvpInfoHidden)]
        public int? PvpDuelLoses { get; set; }

        [AoMember(37)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasNone, (int)InfoPacketFlags.HasPvpInfoHidden)]
        public int? PvpProfessionDuelLoses { get; set; }

        [AoMember(38)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasNone, (int)InfoPacketFlags.HasPvpInfoHidden)]
        public int? PvpSoloKills { get; set; }

        [AoMember(39)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasNone, (int)InfoPacketFlags.HasPvpInfoHidden)]
        public int? PvpTeamKills { get; set; }

        [AoMember(40)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasNone, (int)InfoPacketFlags.HasPvpInfoHidden)]
        public int? PvpSoloScore { get; set; }

        [AoMember(41)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasNone, (int)InfoPacketFlags.HasPvpInfoHidden)]
        public int? PvpTeamScore { get; set; }

        [AoMember(42)]
        [AoUsesFlags("flags", typeof(int), FlagsCriteria.HasNone, (int)InfoPacketFlags.HasPvpInfoHidden)]
        public int? PvpDuelScore { get; set; }

        #endregion
    }
}
