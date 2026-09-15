namespace ZoneEngine_New.Core.Helpers
{
    using System;
    using System.Linq;
    using ZoneEngine_New.Core.Inventory;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Resolves Martial Arts fist low/high AOID and QL from profession + MA skill
    /// (AO-Universe Martial Art Skill Explained tiers).
    /// </summary>
    public static class MartialArtsFistResolver
    {
        public static (int LowId, int HighId, int Quality) Resolve(Profession profession, int martialArtsSkill,
            ItemBehaviorContent? content = null)
        {
            int skill = Math.Clamp(martialArtsSkill < 1 ? 1 : martialArtsSkill, 1, 3000);
            int tier = skill <= 1000 ? 1 : skill <= 2000 ? 2 : 3;
            int skillInTier = ((skill - 1) % 1000) + 1;
            int quality = 1 + (skillInTier - 1) * 499 / 999;
            quality = Math.Clamp(quality, 1, 500);

            content ??= ItemBehaviorContent.Current;
            var weapon = content.FistWeapons.SingleOrDefault(value => value.Profession == profession && value.Tier == tier)
                ?? content.FistWeapons.SingleOrDefault(value => value.Profession == null && value.Tier == tier)
                ?? throw new InvalidOperationException("No configured unarmed weapon for profession and skill tier.");
            return (weapon.LowId, weapon.HighId, quality);
        }
    }
}
