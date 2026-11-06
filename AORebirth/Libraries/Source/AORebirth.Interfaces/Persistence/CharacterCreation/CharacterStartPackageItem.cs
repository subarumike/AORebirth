namespace AORebirth.Interfaces.Persistence.CharacterCreation
{
    using System;
    using System.Collections.Generic;

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
            Variants = new Variant[0];
        }

        public CharacterStartPackageItem(int placement, int quality, int count, IReadOnlyList<Variant> variants)
            : this(placement, 0, 0, quality, count)
        {
            Variants = variants ?? throw new ArgumentNullException(nameof(variants));
            if (variants.Count == 0)
                throw new ArgumentException("Starting item variants must not be empty.", nameof(variants));
        }

        public int Placement { get; }

        public int LowId { get; }

        public int HighId { get; }

        public int Quality { get; }

        public int Count { get; }

        public IReadOnlyList<Variant> Variants { get; }

        internal CharacterStartPackageItem ResolveForGender(int gender)
        {
            if (Variants.Count == 0)
                return this;

            Variant selected = null;
            foreach (Variant variant in Variants)
            {
                if (variant.Gender != gender)
                    continue;
                if (selected != null)
                    throw new InvalidOperationException("Starting item has multiple variants for gender " + gender + ".");
                selected = variant;
            }

            if (selected == null)
                throw new InvalidOperationException("Starting item has no variant for gender " + gender + ".");

            return new CharacterStartPackageItem(Placement, selected.LowId, selected.HighId, Quality, Count);
        }

        /// <summary>Item identities for one gender; quality, count and placement come from the containing entry.</summary>
        public sealed class Variant
        {
            public Variant(int gender, int lowId, int highId)
            {
                Gender = gender;
                LowId = lowId;
                HighId = highId;
            }

            public int Gender { get; }
            public int LowId { get; }
            public int HighId { get; }
        }
    }
}
