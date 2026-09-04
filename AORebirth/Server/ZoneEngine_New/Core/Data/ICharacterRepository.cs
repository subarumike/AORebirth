namespace ZoneEngine_New.Core.Data
{
    public interface ICharacterRepository
    {
        /// <summary>Returns null when no characters row matches <paramref name="characterId"/>.</summary>
        CharacterRecord? GetById(int characterId);
    }
}
