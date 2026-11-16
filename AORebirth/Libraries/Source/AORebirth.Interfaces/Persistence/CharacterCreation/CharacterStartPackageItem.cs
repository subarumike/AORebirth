namespace AORebirth.Interfaces.Persistence.CharacterCreation
{
    using System.Collections.Generic;

    /// <summary>One inventory row granted at character create.</summary>
    public sealed class CharacterStartPackageItem
    {
        readonly IReadOnlyDictionary<int, int> _genderTemplates;

        public CharacterStartPackageItem(int placement, int lowId, int highId, int quality, int count)
            : this(placement, lowId, highId, quality, count, null)
        {
        }

        public CharacterStartPackageItem(int placement, int lowId, int highId, int quality, int count,
            IReadOnlyDictionary<int, int> genderTemplates)
        {
            Placement = placement;
            LowId = lowId;
            HighId = highId;
            Quality = quality;
            Count = count;
            _genderTemplates = genderTemplates;
        }

        public int Placement { get; }

        public int LowId { get; }

        public int HighId { get; }

        public int Quality { get; }

        public int Count { get; }

        internal CharacterStartPackageItem ResolveForGender(int gender)
            => _genderTemplates != null && _genderTemplates.TryGetValue(gender, out int templateId)
                ? new CharacterStartPackageItem(Placement, templateId, templateId, Quality, Count)
                : this;
    }
}
