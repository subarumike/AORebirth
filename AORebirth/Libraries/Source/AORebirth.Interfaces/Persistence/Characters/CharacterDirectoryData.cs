namespace AORebirth.Interfaces.Persistence.Characters
{
    /// <summary>Only the fields required by existing identity/directory consumers; not a character aggregate.</summary>
    public sealed class CharacterDirectoryData
    {
        public int CharacterId { get; set; }
        public string AccountUsername { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Playfield { get; set; }

        /// <summary>Preserves null, zero, one and other persisted values without a Boolean conversion.</summary>
        public int? Online { get; set; }
    }
}
