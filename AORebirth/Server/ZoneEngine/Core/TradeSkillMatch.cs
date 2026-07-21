namespace ZoneEngine.Core
{
    using AORebirth.Core.Items;

    /// <summary>
    /// Result of resolving a tradeskill pair. When <see cref="Swapped"/> is true, the client
    /// put DB Id2 (implant) in Source and DB Id1 (cluster) in Target — skill/QL checks and
    /// DeleteFlag bits must use the DB orientation, not the UI slots.
    /// </summary>
    public sealed class TradeSkillMatch
    {
        public TradeSkillEntry Entry { get; set; }

        public bool Swapped { get; set; }

        public Item ClusterItem(Item sourceItem, Item targetItem)
        {
            return this.Swapped ? targetItem : sourceItem;
        }

        public Item ImplantOrTargetItem(Item sourceItem, Item targetItem)
        {
            return this.Swapped ? sourceItem : targetItem;
        }
    }
}
