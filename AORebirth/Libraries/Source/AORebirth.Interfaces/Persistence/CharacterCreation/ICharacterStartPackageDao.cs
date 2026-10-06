namespace AORebirth.Interfaces.Persistence.CharacterCreation
{
    using System.Collections.Generic;

    /// <summary>
    /// Character-create start packages (per-profession inventory + optional uploaded nanos).
    /// Implementations own GameData access; callers never embed package tables.
    /// </summary>
    public interface ICharacterStartPackageDao
    {
        /// <summary>Loads every profession package from GameData.</summary>
        CharacterStartPackageCatalog LoadCatalog();

        /// <summary>Returns the package for <paramref name="professionId"/>, or null when absent.</summary>
        CharacterStartProfessionPackage TryGetPackage(int professionId);
    }
}
