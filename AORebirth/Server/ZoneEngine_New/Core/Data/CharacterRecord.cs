namespace ZoneEngine_New.Core.Data
{
    /// <summary>
    /// Minimal characters-row projection for zone login (no schema changes).
    /// Built once by the repository and never mutated afterwards.
    /// </summary>
    public sealed class CharacterRecord
    {
        public int Id { get; init; }

        public string? Name { get; init; }

        public int Playfield { get; init; }

        public float X { get; init; }

        public float Y { get; init; }

        public float Z { get; init; }

        public float HeadingW { get; init; }

        public float HeadingX { get; init; }

        public float HeadingY { get; init; }

        public float HeadingZ { get; init; }
    }
}
