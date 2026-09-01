namespace AORebirth.Core.Playfields
{
    using System;

    /// <summary>
    /// ACG treasure chest optional shadow-crystal drop (DB itemnames families).
    /// 20% chance per chest open/respawn for one crystal QL 25-70.
    /// </summary>
    internal static class NascenceTreasureShadowCrystalLoot
    {
        private const int CrystalDropChancePercent = 20;

        private const int CrystalQualityMin = 25;

        private const int CrystalQualityMax = 70;

        // itemnames families (sync via tools-temp/_sync_nascence_mobtemplate.py).
        private static readonly int[] CrackedCrystalLowIds =
        {
            220519, 222492, 222485, 222484, 222483, 221988, 221991, 222193,
            222187, 222186, 222185, 222184, 222183, 222182, 222181, 222180
        };

        private static readonly int[] MiskeptCrystalLowIds =
        {
            222293, 222292, 222291, 222290, 222289, 222288, 222287, 222286,
            222285, 222284, 222283, 222282, 222281, 222280, 222279, 222278
        };

        private static readonly int[] TaintedCrystalLowIds =
        {
            222594, 222593, 222592, 222591, 222590, 222589, 222588, 222587,
            222586, 222585, 222584, 222583, 222582, 222394, 222393, 222392
        };

        internal static bool TryRollCrystal(int containerInstance, int generation, out int lowId, out int highId, out int quality)
        {
            lowId = 0;
            highId = 0;
            quality = 0;
            var rng = new Random(
                unchecked(Environment.TickCount * 397)
                ^ containerInstance
                ^ generation
                ^ (int)DateTime.UtcNow.Ticks);

            if (rng.Next(100) >= CrystalDropChancePercent)
            {
                return false;
            }

            int family = rng.Next(3);
            int[] pool;
            switch (family)
            {
                case 0:
                    pool = CrackedCrystalLowIds;
                    break;
                case 1:
                    pool = MiskeptCrystalLowIds;
                    break;
                default:
                    pool = TaintedCrystalLowIds;
                    break;
            }

            if (pool.Length == 0)
            {
                return false;
            }

            lowId = pool[rng.Next(pool.Length)];
            highId = lowId;
            quality = rng.Next(CrystalQualityMin, CrystalQualityMax + 1);
            return true;
        }
    }
}
