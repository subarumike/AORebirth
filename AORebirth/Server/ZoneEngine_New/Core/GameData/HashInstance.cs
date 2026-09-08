namespace ZoneEngine_New.Core.GameData
{
    using System;

    /// <summary>One concrete item family from GameData/HashInstances.json.</summary>
    public sealed class HashInstance
    {
        public HashInstance(string hash, int[] templateIds, int minLevel, int maxLevel)
        {
            ArgumentException.ThrowIfNullOrEmpty(hash);
            ArgumentNullException.ThrowIfNull(templateIds);
            if (templateIds.Length == 0)
                throw new ArgumentException("At least one TemplateId is required.", nameof(templateIds));

            Hash = hash;
            TemplateIds = templateIds;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
        }

        public string Hash { get; }

        public int[] TemplateIds { get; }

        public int MinLevel { get; }

        public int MaxLevel { get; }
    }
}
