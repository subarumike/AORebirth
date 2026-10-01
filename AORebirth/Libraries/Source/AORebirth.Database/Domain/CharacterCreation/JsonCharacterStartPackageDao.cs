namespace AORebirth.Database.Domain.CharacterCreation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    using AORebirth.Interfaces.Persistence.CharacterCreation;

    /// <summary>
    /// Content DAO for <c>GameData/StartPackages.json</c>. Profession create kits stay out of C#.
    /// </summary>
    public sealed class JsonCharacterStartPackageDao : ICharacterStartPackageDao
    {
        public const string FileName = "StartPackages.json";

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
                using (var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var serializer = new DataContractJsonSerializer(typeof(Document));
                    document = serializer.ReadObject(stream) as Document;
                }

                if (document == null || document.Packages == null)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Character start packages empty or invalid: {0}",
                            _path));
                }

                var packages = new List<CharacterStartProfessionPackage>(document.Packages.Length);
                for (int i = 0; i < document.Packages.Length; i++)
                {
                    PackageRow row = document.Packages[i];
                    if (row == null || row.ProfessionId <= 0)
                        continue;

                    var items = new List<CharacterStartPackageItem>();
                    if (row.Items != null)
                    {
                        for (int j = 0; j < row.Items.Length; j++)
                        {
                            ItemRow item = row.Items[j];
                            if (item == null || item.LowId <= 0 || item.HighId <= 0 || item.Placement <= 0)
                                continue;

                            items.Add(
                                new CharacterStartPackageItem(
                                    item.Placement,
                                    item.LowId,
                                    item.HighId,
                                    item.Quality > 0 ? item.Quality : 1,
                                    item.Count > 0 ? item.Count : 1));
                        }
                    }

                    var nanos = new List<int>();
                    if (row.UploadedNanos != null)
                    {
                        for (int j = 0; j < row.UploadedNanos.Length; j++)
                        {
                            int nanoId = row.UploadedNanos[j];
                            if (nanoId > 0 && !nanos.Contains(nanoId))
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

                _cached = new CharacterStartPackageCatalog(packages);
                return _cached;
            }
        }

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
