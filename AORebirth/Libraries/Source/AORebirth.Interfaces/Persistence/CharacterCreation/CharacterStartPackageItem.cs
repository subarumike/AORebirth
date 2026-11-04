namespace AORebirth.Interfaces.Persistence.CharacterCreation
{
    /// <summary>One inventory row granted at character create.</summary>
    public sealed class CharacterStartPackageItem
    {
        public CharacterStartPackageItem(int placement, int lowId, int highId, int quality, int count)
            : this(placement, lowId, highId, quality, count, null)
        {
        }

        public CharacterStartPackageItem(int placement, int lowId, int highId, int quality, int count, int? gender)
        {
            Placement = placement;
            LowId = lowId;
            HighId = highId;
            Quality = quality;
            Count = count;
            Gender = gender;
        }

        public int Placement { get; }

        public int LowId { get; }

        public int HighId { get; }

        public int Quality { get; }

        public int Count { get; }

        /// <summary>Required character gender, or null for an unconditional starter row.</summary>
        public int? Gender { get; }
    }
}
