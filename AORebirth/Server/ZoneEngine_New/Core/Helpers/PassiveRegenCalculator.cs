namespace ZoneEngine_New.Core.Helpers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// AO-Universe breed statistics §4: natural health/nano delta and tick intervals.
    /// Stored HealDelta / NanoDelta (gear, buffs, and explicit sets) are added on top.
    /// Health trickle is Body Development / 100. Nano trickle is Nano Pool / 100.
    /// Sitting halves the interval. Non-playable breeds have a 0 breed base.
    /// </summary>
    public static class PassiveRegenCalculator
    {
        public static int ComputeHealthDelta(int breed, int bodyDevelopment, int healDeltaStat = 0)
            => BreedHealthBase(breed) + PerHundred(bodyDevelopment) + healDeltaStat;

        public static int ComputeNanoDelta(int breed, int nanoPool, int nanoDeltaStat = 0)
            => BreedNanoBase(breed) + PerHundred(nanoPool) + nanoDeltaStat;

        public static double ComputeHealthIntervalSeconds(int stamina, bool sitting)
        {
            int standing = Math.Max(2, 29 - Math.Max(0, stamina) / 30);
            return sitting ? standing / 2.0 : standing;
        }

        public static double ComputeNanoIntervalSeconds(int psychic, bool sitting)
        {
            int standing = Math.Max(2, 28 - (2 * (Math.Max(0, psychic) / 60)));
            return sitting ? standing / 2.0 : standing;
        }

        static int PerHundred(int value)
            => Math.Max(0, value) / 100;

        static int BreedHealthBase(int breed)
        {
            return (Breed)breed switch
            {
                Breed.Solitus => 3,
                Breed.Opifex => 3,
                Breed.Nanomage => 2,
                Breed.Atrox => 4,
                _ => 0
            };
        }

        static int BreedNanoBase(int breed)
        {
            return (Breed)breed switch
            {
                Breed.Solitus => 3,
                Breed.Opifex => 3,
                Breed.Nanomage => 4,
                Breed.Atrox => 2,
                _ => 0
            };
        }
    }
}
