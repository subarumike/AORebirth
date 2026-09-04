namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    public interface IStatRepository
    {
        IReadOnlyList<StatRecord> GetForCharacter(int characterId);
    }
}
