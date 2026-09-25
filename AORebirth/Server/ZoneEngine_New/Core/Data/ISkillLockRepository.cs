namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    /// <summary>One persisted LockSkill cooldown. Expiry is absolute so a relog cannot refresh it.</summary>
    public sealed class SkillLockRecord
    {
        public int StatId { get; init; }

        public long ExpiresAtUtcTicks { get; init; }
    }

    public interface ISkillLockRepository
    {
        IReadOnlyList<SkillLockRecord> GetForCharacter(int characterId);
    }
}
