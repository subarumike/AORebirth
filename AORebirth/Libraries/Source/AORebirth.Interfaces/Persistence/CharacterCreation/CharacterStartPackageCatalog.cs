namespace AORebirth.Interfaces.Persistence.CharacterCreation
{
    using System.Collections.Generic;

    /// <summary>Detached catalog of all profession start packages.</summary>
    public sealed class CharacterStartPackageCatalog
    {
        public CharacterStartPackageCatalog(IReadOnlyList<CharacterStartProfessionPackage> packages)
        {
            Packages = packages ?? new CharacterStartProfessionPackage[0];
        }

        public IReadOnlyList<CharacterStartProfessionPackage> Packages { get; }
    }
}
