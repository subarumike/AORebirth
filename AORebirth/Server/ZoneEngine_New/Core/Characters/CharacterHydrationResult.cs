namespace ZoneEngine_New.Core.Characters
{
    using System.Collections.Generic;

    using ZoneEngine_New.Core.Data;

    public sealed class CharacterHydrationResult
    {
        public CharacterRecord Character { get; init; } = null!;

        public IReadOnlyList<StatRecord> Stats { get; init; } = [];

        public IReadOnlyList<ItemInstanceRecord> Items { get; init; } = [];

        public bool IsSpawnReady => Character != null && Stats.Count > 0;
    }
}
