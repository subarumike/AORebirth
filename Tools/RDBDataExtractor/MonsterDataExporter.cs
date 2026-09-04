namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using AODB;
    using AODB.Common.Enums;
    using AODB.Common.RDBObjects;
    using AORebirth.Core.GameData;

    /// <summary>
    /// Exports MonsterData id → CatMesh id pairings from RDB MonsterData records.
    /// CatMesh comes from MonsterData stat <see cref="StatId.mesh"/> (12), which holds
    /// the CatMesh resource id (see docs/reference/enemies/EnemyNpcDllAodbMap.md).
    /// </summary>
    internal sealed class MonsterDataExporter
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly RdbController controller;
        private readonly string gameDataDirectory;

        internal MonsterDataExporter(RdbController controller, string gameDataDirectory)
        {
            if (controller == null)
                throw new ArgumentNullException("controller");
            if (string.IsNullOrWhiteSpace(gameDataDirectory))
                throw new ArgumentException("GameData directory is required.", "gameDataDirectory");

            this.controller = controller;
            this.gameDataDirectory = gameDataDirectory;
        }

        internal bool HasMonsterDataRecordType()
        {
            return this.controller.RecordTypeToId.ContainsKey((int)ResourceTypeId.MonsterData);
        }

        /// <summary>
        /// Writes MonsterData.json under the GameData root. Existing files are skipped
        /// unless <paramref name="overwrite"/> is true.
        /// </summary>
        internal ExportFileCounts Export(bool overwrite)
        {
            string path = Path.Combine(
                this.gameDataDirectory,
                GameDataPaths.MonsterDataFileName);

            if (!overwrite && File.Exists(path))
                return new ExportFileCounts(0, 1);

            if (!this.HasMonsterDataRecordType())
            {
                throw new InvalidOperationException(
                    "RDB MonsterData record type "
                    + (int)ResourceTypeId.MonsterData
                    + " was not found.");
            }

            List<MonsterDataCatMeshPairing> pairings = this.BuildPairings();
            Directory.CreateDirectory(this.gameDataDirectory);
            File.WriteAllText(path, JsonSerializer.Serialize(pairings, JsonOptions));

            Console.WriteLine(
                "exported "
                + GameDataPaths.MonsterDataFileName
                + " pairings="
                + pairings.Count);
            return new ExportFileCounts(1, 0);
        }

        private List<MonsterDataCatMeshPairing> BuildPairings()
        {
            IEnumerable<int> ids = this.controller
                .RecordTypeToId[(int)ResourceTypeId.MonsterData]
                .Keys
                .OrderBy(id => id);

            var pairings = new List<MonsterDataCatMeshPairing>();
            foreach (int monsterDataId in ids)
            {
                MonsterData record = this.controller.Get<MonsterData>(
                    ResourceTypeId.MonsterData,
                    monsterDataId);
                if (record == null || record.Stats == null)
                    continue;

                uint catMesh;
                if (!record.Stats.TryGetValue((int)StatId.mesh, out catMesh) || catMesh == 0)
                    continue;

                pairings.Add(
                    new MonsterDataCatMeshPairing
                    {
                        MonsterData = monsterDataId,
                        CatMesh = (int)catMesh,
                    });
            }

            return pairings;
        }

        private sealed class MonsterDataCatMeshPairing
        {
            public int MonsterData { get; set; }

            public int CatMesh { get; set; }
        }
    }
}
