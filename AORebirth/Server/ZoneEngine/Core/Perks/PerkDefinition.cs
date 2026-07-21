namespace ZoneEngine.Core.Perks
{
    /// <summary>
    /// Static perk row from Perks.xml plus optional action grant data from items.dat / PerkActions.csv.
    /// </summary>
    public sealed class PerkDefinition
    {
        public int PacketId { get; set; }

        public int Aoid { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// When set, training this perk sends AddPerkAction (0xB4) for the Perk Actions menu.
        /// </summary>
        public int? ActionTemplateId { get; set; }

        /// <summary>
        /// Four-character action hash (wire Parameter2), e.g. QUBS / CNRE.
        /// </summary>
        public int? ActionHash { get; set; }

        /// <summary>
        /// AddAction / AddPerkAction Parameter1 from items.dat (often 10000+PacketID; sometimes differs).
        /// </summary>
        public int? ActionSlotIdOverride { get; set; }

        public bool GrantsPerkAction
        {
            get
            {
                return this.ActionTemplateId.HasValue && this.ActionHash.HasValue;
            }
        }

        public int ActionSlotId
        {
            get
            {
                if (this.ActionSlotIdOverride.HasValue && this.ActionSlotIdOverride.Value > 0)
                {
                    return this.ActionSlotIdOverride.Value;
                }

                return 10000 + this.PacketId;
            }
        }
    }
}
