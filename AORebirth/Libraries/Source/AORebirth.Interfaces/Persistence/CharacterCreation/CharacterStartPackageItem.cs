namespace AORebirth.Interfaces.Persistence.CharacterCreation
{
    /// <summary>One inventory row granted at character create.</summary>
    public sealed class CharacterStartPackageItem
    {
        public CharacterStartPackageItem(int placement, int lowId, int highId, int quality, int count)
        {
            Placement = placement;
            LowId = lowId;
            HighId = highId;
            Quality = quality;
            Count = count;
        }

        public int Placement { get; }

        public int LowId { get; }

        public int HighId { get; }

        public int Quality { get; }

        public int Count { get; }
    }
}
