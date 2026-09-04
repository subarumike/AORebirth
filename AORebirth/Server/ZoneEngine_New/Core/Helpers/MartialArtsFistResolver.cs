namespace ZoneEngine_New.Core.Helpers
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Resolves Martial Arts fist low/high AOID and QL from profession + MA skill
    /// (AO-Universe Martial Art Skill Explained tiers).
    /// </summary>
    public static class MartialArtsFistResolver
    {
        public static (int LowId, int HighId, int Quality) Resolve(Profession profession, int martialArtsSkill)
        {
            int skill = Math.Clamp(martialArtsSkill < 1 ? 1 : martialArtsSkill, 1, 3000);
            int tier = skill <= 1000 ? 1 : skill <= 2000 ? 2 : 3;
            int skillInTier = ((skill - 1) % 1000) + 1;
            int quality = 1 + (skillInTier - 1) * 499 / 999;
            quality = Math.Clamp(quality, 1, 500);

            (int lowId, int highId) = ResolveIds(profession, tier);
            return (lowId, highId, quality);
        }

        static (int LowId, int HighId) ResolveIds(Profession profession, int tier)
        {
            if (profession == Profession.MartialArtist)
            {
                return tier switch
                {
                    1 => (211352, 211366),
                    2 => (211357, 211358),
                    _ => (211363, 211364)
                };
            }

            if (profession == Profession.Shade)
            {
                return tier switch
                {
                    1 => (211349, 211351),
                    2 => (211359, 211360),
                    _ => (211365, 211366)
                };
            }

            return tier switch
            {
                1 => (43712, 43713),
                2 => (211355, 211356),
                _ => (211361, 211362)
            };
        }
    }
}
