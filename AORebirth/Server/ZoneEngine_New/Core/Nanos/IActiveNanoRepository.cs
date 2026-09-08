namespace ZoneEngine_New.Core.Nanos
{
    using System.Collections.Generic;
    using ZoneEngine_New.Core.Data;

    public sealed record ActiveNanoRecord(int NanoId, int Strain, int NanoInstance,
        int DurationCentiseconds, long ExpiresAtUtcTicks);

    public sealed record NanoCharacterWrite(int CharacterId, IReadOnlyList<ActiveNanoRecord> ActiveNanos,
        IReadOnlyList<StatRecord> BaseStats);

    /// <summary>Existing charactersactivenanos ownership; no parallel store or runtime schema creation.</summary>
    public interface IActiveNanoRepository
    {
        IReadOnlyList<ActiveNanoRecord> Load(int characterId);
        // A caster and affected player may both have durable state; commit that transition once.
        void Commit(IReadOnlyList<NanoCharacterWrite> characters);
    }
}
