namespace ZoneEngine.Core.Missions
{
    using System;

    /// <summary>
    /// Ultra-rare mission interior loot (NOT terminal roll rewards). Mike: ~1% on mobs/chests;
    /// more entries will be added later. Seeded with Instruction Disc (Summon Grid Armor) Mk I–IV
    /// (aogalaxy AOIDs 155198–155201).
    /// </summary>
    internal static class MissionRareLootCatalog
    {
        internal const int DropChancePercent = 1;

        internal sealed class RareDrop
        {
            public int LowId;

            public int HighId;

            public int Quality;

            public string Name;
        }

        // AO Galaxy: Instruction Disc (Summon Grid Armor Mk I/II/III/IV).
        private static readonly RareDrop[] Drops =
            {
                new RareDrop
                {
                    LowId = 155198,
                    HighId = 155198,
                    Quality = 60,
                    Name = "Instruction Disc (Summon Grid Armor Mk I)"
                },
                new RareDrop
                {
                    LowId = 155200,
                    HighId = 155200,
                    Quality = 93,
                    Name = "Instruction Disc (Summon Grid Armor Mk II)"
                },
                new RareDrop
                {
                    LowId = 155199,
                    HighId = 155199,
                    Quality = 116,
                    Name = "Instruction Disc (Summon Grid Armor Mk III)"
                },
                new RareDrop
                {
                    LowId = 155201,
                    HighId = 155201,
                    Quality = 140,
                    Name = "Instruction Disc (Summon Grid Armor Mk IV)"
                }
            };

        /// <summary>
        /// ~1% chance to pick one rare disc. Prefer discs whose QL is within missionQl ± 20;
        /// fall back to any listed disc.
        /// </summary>
        public static bool TryRoll(int missionQl, Random rng, out RareDrop drop)
        {
            drop = null;
            if (rng == null || Drops.Length == 0)
            {
                return false;
            }

            if (rng.Next(100) >= DropChancePercent)
            {
                return false;
            }

            RareDrop best = null;
            int bestDist = int.MaxValue;
            for (int i = 0; i < Drops.Length; i++)
            {
                RareDrop candidate = Drops[i];
                int dist = Math.Abs(candidate.Quality - Math.Max(1, missionQl));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }

            // If several are close, pick randomly among those within ±20.
            var near = new System.Collections.Generic.List<RareDrop>();
            for (int i = 0; i < Drops.Length; i++)
            {
                if (Math.Abs(Drops[i].Quality - Math.Max(1, missionQl)) <= 20)
                {
                    near.Add(Drops[i]);
                }
            }

            if (near.Count > 0)
            {
                drop = near[rng.Next(near.Count)];
                return true;
            }

            drop = best;
            return drop != null;
        }

        public static bool IsRareLootTemplate(int lowId)
        {
            for (int i = 0; i < Drops.Length; i++)
            {
                if (Drops[i].LowId == lowId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
