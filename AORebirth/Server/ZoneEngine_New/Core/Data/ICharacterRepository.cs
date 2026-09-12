namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    public interface ICharacterRepository
    {
        /// <summary>Returns null when no characters row matches <paramref name="characterId"/>.</summary>
        CharacterRecord? GetById(int characterId);

        /// <summary>Marks the row online while the shared cross-engine ownership lease is held.</summary>
        void SetOnline(int characterId);

        /// <summary>Clears online state after the ownership guard confirms there is no zone owner.</summary>
        void SetOffline(int characterId);

        /// <summary>
        /// Writes playfield, transform, and online flag. Does not touch name or other character columns.
        /// </summary>
        void SaveLocation(CharacterRecord character, int online);

        /// <summary>Atomically persists location, online state, and base stats.</summary>
        void SaveSnapshot(CharacterRecord character, int online, IReadOnlyList<StatRecord> stats);
    }
}
