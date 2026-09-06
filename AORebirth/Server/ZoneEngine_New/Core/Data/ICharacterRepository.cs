namespace ZoneEngine_New.Core.Data
{
    public interface ICharacterRepository
    {
        /// <summary>Returns null when no characters row matches <paramref name="characterId"/>.</summary>
        CharacterRecord? GetById(int characterId);

        /// <summary>
        /// Writes playfield, transform, and online flag. Does not touch name or other character columns.
        /// </summary>
        void SaveLocation(CharacterRecord character, int online);
    }
}
