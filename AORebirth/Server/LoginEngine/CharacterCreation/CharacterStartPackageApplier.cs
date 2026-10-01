namespace LoginEngine.CharacterCreation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using AORebirth.Core.GameData;
    using AORebirth.Database;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Domain.CharacterCreation;
    using AORebirth.Interfaces;
    using AORebirth.Interfaces.Persistence.CharacterCreation;
    using AORebirth.Interfaces.Persistence.Characters;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Applies GameData start packages at character create through
    /// <see cref="ICharacterStartPackageDao"/> and <see cref="ICharacterPersistenceDao"/>.
    /// </summary>
    internal static class CharacterStartPackageApplier
    {
        /// <summary>Matches existing capture-backed starter rows in <c>item_instances.Source</c>.</summary>
        const int StarterItemSource = 1;

        static readonly object Gate = new object();
        static ICharacterStartPackageDao _packages;

        public static void Apply(int characterId, int professionId)
        {
            if (characterId <= 0 || professionId <= 0)
                return;

            CharacterStartProfessionPackage package = ResolvePackages().TryGetPackage(professionId);
            if (package == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "No start package for professionId={0}",
                        professionId));
            }

            IReadOnlyList<CharacterStartPackageItem> items = package.Items ?? Array.Empty<CharacterStartPackageItem>();
            IReadOnlyList<int> nanos = package.UploadedNanos ?? Array.Empty<int>();
            if (items.Count == 0 && nanos.Count == 0)
                return;

            ICharacterPersistenceDao persistence = DatabaseDaoFactory.CreateCharacterPersistenceDao();
            var inserts = new List<PersistedItemData>(items.Count);
            if (items.Count > 0)
            {
                int firstId = persistence.LeaseItemInstanceIds(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    CharacterStartPackageItem entry = items[i];
                    inserts.Add(
                        new PersistedItemData
                        {
                            InstanceId = firstId + i,
                            // ZE_New hydration: ContainerType = page, ContainerInstance = characterId.
                            ContainerType = (int)IdentityType.Inventory,
                            ContainerInstance = characterId,
                            ContainerPlacement = entry.Placement,
                            ItemType = 0,
                            LowId = entry.LowId,
                            HighId = entry.HighId,
                            Quality = entry.Quality,
                            StackCount = entry.Count,
                            Source = StarterItemSource
                        });
                }
            }

            var uploaded = new List<int>(nanos.Count);
            for (int i = 0; i < nanos.Count; i++)
            {
                int nanoId = nanos[i];
                if (nanoId > 0 && !uploaded.Contains(nanoId))
                    uploaded.Add(nanoId);
            }

            persistence.SaveInventoryAndUploadedNanos(
                characterId,
                inserts,
                new List<ItemLocationData>(),
                uploaded);
        }

        static ICharacterStartPackageDao ResolvePackages()
        {
            lock (Gate)
            {
                if (_packages != null)
                    return _packages;

                string root = GameDataPaths.ResolveRuntimeRoot();
                string path = System.IO.Path.Combine(root, GameDataPaths.StartPackagesFileName);
                if (!System.IO.File.Exists(path))
                {
                    string fallback = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        GameDataPaths.RootFolderName);
                    string fallbackPath = System.IO.Path.Combine(fallback, GameDataPaths.StartPackagesFileName);
                    if (System.IO.File.Exists(fallbackPath))
                        root = fallback;
                }

                _packages = new JsonCharacterStartPackageDao(root);
                return _packages;
            }
        }
    }
}
