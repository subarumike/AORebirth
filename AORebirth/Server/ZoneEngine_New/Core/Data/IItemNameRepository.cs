namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    public interface IItemNameRepository
    {
        bool TryGetName(int aoid, out string name);

        IReadOnlyDictionary<int, string> GetAllNames();
    }
}
