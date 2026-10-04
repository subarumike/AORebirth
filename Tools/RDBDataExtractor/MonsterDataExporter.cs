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
    /// Exports per-MonsterData body data from RDB MonsterData records: the CatMesh id (stat
    /// <see cref="StatId.mesh"/>, 12; see docs/reference/enemies/EnemyNpcDllAodbMap.md), Mass (stat 2) and
    /// CharRadius (stat 421), the radius the client's weapon range check uses (CharRadius * Scale / 100,
    /// Gamecode.dll 0x10044e56).
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

                // Records without a mesh still carry Mass and CharRadius, so every record is written (catMesh 0).
                uint catMesh;
                if (!record.Stats.TryGetValue((int)StatId.mesh, out catMesh))
                    catMesh = 0;

                uint mass;
                uint charRadius;
                pairings.Add(
                    new MonsterDataCatMeshPairing
                    {
                        MonsterData = monsterDataId,
                        CatMesh = (int)catMesh,
                        Mass = record.Stats.TryGetValue((int)StatId.volumemass, out mass) ? (int?)mass : null,
                        CharRadius = record.Stats.TryGetValue((int)StatId.charradius, out charRadius) ? (int?)charRadius : null,
                    });
            }

            return pairings;
        }

        private sealed class MonsterDataCatMeshPairing
        {
            public int MonsterData { get; set; }

            public int CatMesh { get; set; }

            /// <summary>Stat 2 (Mass).</summary>
            public int? Mass { get; set; }

            /// <summary>Stat 421 (CharRadius).</summary>
            public int? CharRadius { get; set; }
        }
    }
}
