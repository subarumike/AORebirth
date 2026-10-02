namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    /// <summary>
    /// LockPerk cooldowns (characterperklocks). Records reuse <see cref="SkillLockRecord"/>: StatId holds the
    /// locked perk id.
    /// </summary>
    public interface IPerkLockRepository
    {
        IReadOnlyList<SkillLockRecord> GetForCharacter(int characterId);

        /// <summary>Replaces the character's stored perk locks with <paramref name="perkLocks"/>.</summary>
        void Save(int characterId, IReadOnlyList<SkillLockRecord> perkLocks);
    }
}
