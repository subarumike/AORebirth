namespace ZoneEngine_New.Core.Data
{
    using System.Collections.Generic;

    /// <summary>Trained perk ids (charactersperks).</summary>
    public interface ITrainedPerkRepository
    {
        IReadOnlyList<int> GetForCharacter(int characterId);

        /// <summary>Replaces the character's stored trained perks with <paramref name="perkIds"/>.</summary>
        void Save(int characterId, IReadOnlyList<int> perkIds);
    }
}
