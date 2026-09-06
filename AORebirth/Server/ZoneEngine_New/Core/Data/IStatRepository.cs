namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    public interface IStatRepository
    {
        IReadOnlyList<StatRecord> GetForCharacter(int characterId);

        /// <summary>
        /// Inserts or updates base stats for the character. Does not delete other rows for the same owner.
        /// Empty <paramref name="stats"/> is a no-op.
        /// </summary>
        void UpsertForCharacter(int characterId, IReadOnlyList<StatRecord> stats);
    }
}
