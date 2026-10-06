namespace AORebirth.Database.Domain.CharacterCreation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    using AORebirth.Interfaces.Persistence.CharacterCreation;
    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// Content DAO for <c>GameData/StartPackages.json</c>. Profession create kits stay out of C#.
    /// </summary>
    public sealed class JsonCharacterStartPackageDao : ICharacterStartPackageDao
    {
        public const string FileName = "StartPackages.json";

        // Matches the carried inventory page used by PlayerInventoryPage and PlayerInventory.
        const int InventoryFirstSlot = 0x40;
        const int InventorySlotCount = 30;

        readonly string _path;
        readonly object _gate = new object();
        CharacterStartPackageCatalog _cached;

        public JsonCharacterStartPackageDao(string gameDataRoot)
        {
            if (string.IsNullOrWhiteSpace(gameDataRoot))
                throw new ArgumentException("GameData root is required.", nameof(gameDataRoot));

            _path = Path.Combine(gameDataRoot.Trim(), FileName);
        }

        public CharacterStartPackageCatalog LoadCatalog()
        {
            lock (_gate)
            {
                if (_cached != null)
                    return _cached;

                if (!File.Exists(_path))
                {
                    throw new FileNotFoundException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Character start packages missing: {0}",
                            _path),
                        _path);
                }

                Document document;
                try
                {
                    using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(Document));
                        document = serializer.ReadObject(stream) as Document;
                    }
                }
                catch (SerializationException exception)
                {
                    throw new InvalidDataException(_path + ": invalid start package JSON.", exception);
                }

                if (document == null || document.Packages == null || document.Packages.Length == 0)
                    throw InvalidCatalog("packages must be a nonempty array.");

                var packages = new List<CharacterStartProfessionPackage>(document.Packages.Length);
                var professions = new HashSet<int>();
                for (int i = 0; i < document.Packages.Length; i++)
                {
                    PackageRow row = document.Packages[i];
                    if (row == null || !IsPlayerProfession(row.ProfessionId))
                        throw InvalidCatalog("packages[" + i + "] needs a playable professionId.");
                    if (!professions.Add(row.ProfessionId))
                        throw InvalidCatalog("Duplicate professionId=" + row.ProfessionId + ".");
                    if (row.Items == null)
                        throw InvalidCatalog("professionId=" + row.ProfessionId + " needs an items array.");

                    var items = new List<CharacterStartPackageItem>();
                    var placements = new HashSet<int>();
                    for (int j = 0; j < row.Items.Length; j++)
                    {
                        ItemRow item = row.Items[j];
                        if (item == null || item.LowId <= 0 || item.HighId <= 0
                            || item.Quality <= 0 || item.Count <= 0
                            || item.Placement < InventoryFirstSlot
                            || item.Placement >= InventoryFirstSlot + InventorySlotCount)
                            throw InvalidCatalog("professionId=" + row.ProfessionId + ", items[" + j
                                + "] needs positive item IDs, quality and count, and an inventory placement from "
                                + InventoryFirstSlot + " to " + (InventoryFirstSlot + InventorySlotCount - 1) + ".");
                        if (!placements.Add(item.Placement))
                            throw InvalidCatalog("professionId=" + row.ProfessionId
                                + " repeats inventory placement " + item.Placement + ".");

                        items.Add(new CharacterStartPackageItem(
                            item.Placement, item.LowId, item.HighId, item.Quality, item.Count));
                    }

                    var nanos = new List<int>();
                    if (row.UploadedNanos != null)
                    {
                        for (int j = 0; j < row.UploadedNanos.Length; j++)
                        {
                            int nanoId = row.UploadedNanos[j];
                            if (nanoId <= 0 || nanos.Contains(nanoId))
                                throw InvalidCatalog("professionId=" + row.ProfessionId
                                    + " needs positive, unique uploadedNanos IDs.");
                            nanos.Add(nanoId);
                        }
                    }

                    packages.Add(
                        new CharacterStartProfessionPackage(
                            row.ProfessionId,
                            row.Profession ?? string.Empty,
                            row.Evidence ?? string.Empty,
                            items,
                            nanos));
                }

                foreach (Profession profession in Enum.GetValues(typeof(Profession)))
                {
                    if (IsPlayerProfession((int)profession) && !professions.Contains((int)profession))
                        throw InvalidCatalog("Missing start package for professionId=" + (int)profession + ".");
                }

                _cached = new CharacterStartPackageCatalog(packages);
                return _cached;
            }
        }

        InvalidDataException InvalidCatalog(string reason) => new InvalidDataException(_path + ": " + reason);

        static bool IsPlayerProfession(int professionId) => Enum.IsDefined(typeof(Profession), professionId)
            && professionId != (int)Profession.None && professionId != (int)Profession.Monster;

        public CharacterStartProfessionPackage TryGetPackage(int professionId)
        {
            if (professionId <= 0)
                return null;

            CharacterStartPackageCatalog catalog = LoadCatalog();
            for (int i = 0; i < catalog.Packages.Count; i++)
            {
                CharacterStartProfessionPackage package = catalog.Packages[i];
                if (package.ProfessionId == professionId)
                    return package;
            }

            return null;
        }

        [DataContract]
        sealed class Document
        {
            [DataMember(Name = "packages")]
            public PackageRow[] Packages { get; set; }
        }

        [DataContract]
        sealed class PackageRow
        {
            [DataMember(Name = "professionId")]
            public int ProfessionId { get; set; }

            [DataMember(Name = "profession")]
            public string Profession { get; set; }

            [DataMember(Name = "evidence")]
            public string Evidence { get; set; }

            [DataMember(Name = "items")]
            public ItemRow[] Items { get; set; }

            [DataMember(Name = "uploadedNanos")]
            public int[] UploadedNanos { get; set; }
        }

        [DataContract]
        sealed class ItemRow
        {
            [DataMember(Name = "placement")]
            public int Placement { get; set; }

            [DataMember(Name = "lowId")]
            public int LowId { get; set; }

            [DataMember(Name = "highId")]
            public int HighId { get; set; }

            [DataMember(Name = "quality")]
            public int Quality { get; set; }

            [DataMember(Name = "count")]
            public int Count { get; set; }
        }
    }
}
