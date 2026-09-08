namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    public interface IUploadedNanoRepository
    {
        IReadOnlyList<int> GetForCharacter(int characterId);
    }
}
