namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Linq;
    using ZoneEngine_New.Core.Inventory;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Resolves the skill-scaled unarmed items: the Martial Arts fist (profession + MA skill) and the
    /// Brawl Item (Brawl skill). Each 1000 skill is a tier of QL 1-500 templates
    /// (AO-Universe Martial Art Skill Explained tiers).
    /// </summary>
    public static class MartialArtsFistResolver
    {
        public static (int LowId, int HighId, int Quality) Resolve(Profession profession, int martialArtsSkill,
            ItemBehaviorContent? content = null)
        {
            (int tier, int quality) = ResolveSkillTier(martialArtsSkill);

            content ??= ItemBehaviorContent.Current;
            var weapon = content.FistWeapons.SingleOrDefault(value => value.Profession == profession && value.Tier == tier)
                ?? content.FistWeapons.SingleOrDefault(value => value.Profession == null && value.Tier == tier)
                ?? throw new InvalidOperationException("No configured unarmed weapon for profession and skill tier.");
            return SelectPair(weapon, quality);
        }

        /// <summary>The Brawl Item tier and QL for <paramref name="brawlSkill"/>.</summary>
        public static (int LowId, int HighId, int Quality) ResolveBrawl(int brawlSkill, ItemBehaviorContent? content = null)
        {
            (int tier, int quality) = ResolveSkillTier(brawlSkill);

            content ??= ItemBehaviorContent.Current;
            var weapon = content.BrawlWeapons.SingleOrDefault(value => value.Tier == tier)
                ?? throw new InvalidOperationException("No configured brawl item for skill tier.");
            return SelectPair(weapon, quality);
        }

        /// <summary>The Dimach Item for <paramref name="profession"/> (falling back to everyone else) and Dimach skill.</summary>
        public static (int LowId, int HighId, int Quality) ResolveDimach(Profession profession, int dimachSkill,
            ItemBehaviorContent? content = null)
        {
            (int tier, int quality) = ResolveSkillTier(dimachSkill);

            content ??= ItemBehaviorContent.Current;
            var weapon = content.DimachWeapons.SingleOrDefault(value => value.Profession == profession && value.Tier == tier)
                ?? content.DimachWeapons.SingleOrDefault(value => value.Profession == null && value.Tier == tier)
                ?? throw new InvalidOperationException("No configured dimach item for profession and skill tier.");
            return SelectPair(weapon, quality);
        }

        /// <summary>Skill 1-1000 is tier 1, 1001-2000 tier 2, 2001-3000 tier 3; QL 1-500 within the tier.</summary>
        static (int Tier, int Quality) ResolveSkillTier(int rawSkill)
        {
            int skill = Math.Clamp(rawSkill < 1 ? 1 : rawSkill, 1, 3000);
            int tier = skill <= 1000 ? 1 : skill <= 2000 ? 2 : 3;
            int skillInTier = ((skill - 1) % 1000) + 1;
            int quality = 1 + (skillInTier - 1) * 499 / 999;
            return (tier, Math.Clamp(quality, 1, 500));
        }

        /// <summary>A tier with an intermediate template interpolates low→mid up to MidQuality, then mid→high.</summary>
        static (int LowId, int HighId, int Quality) SelectPair(FistWeaponContent weapon, int quality)
        {
            if (weapon.MidId > 0)
                return quality <= weapon.MidQuality
                    ? (weapon.LowId, weapon.MidId, quality)
                    : (weapon.MidId, weapon.HighId, quality);
            return (weapon.LowId, weapon.HighId, quality);
        }
    }
}
