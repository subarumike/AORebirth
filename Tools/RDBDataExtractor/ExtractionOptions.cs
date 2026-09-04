namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.IO;

    using AORebirth.Core.GameData;
    using CommandLine;

    [Verb(
        "extract",
        isDefault: true,
        HelpText = "Export RDB playfield tilemaps/districts and MonsterData→CatMesh pairings.")]
    internal sealed class ExtractionOptions
    {
        [Option(
            "ao-path",
            HelpText = "Path to the installed AO client. Falls back to AO_CLIENT_PATH when omitted.")]
        public string AoClientPath { get; set; }

        [Option(
            "output-dir",
            HelpText = "Playfield output root. Defaults to AORebirth/GameData/Playfields under the repo root.")]
        public string OutputDirectory { get; set; }

        [Option(
            "gamedata-dir",
            HelpText = "GameData root for MonsterData.json. Defaults to AORebirth/GameData under the repo root.")]
        public string GameDataDirectory { get; set; }

        [Option("tilemap-id", HelpText = "Export one playfield resource id (tilemap and/or district).")]
        public int? TilemapId { get; set; }

        [Option(
            "overwrite",
            HelpText = "Replace existing output files. By default each existing file is skipped.")]
        public bool Overwrite { get; set; }

        [Option("self-test", HelpText = "Run built-in exporter self tests.")]
        public bool SelfTest { get; set; }

        [Option(
            "skip-monster-data",
            HelpText = "Skip MonsterData.json export (playfields only).")]
        public bool SkipMonsterData { get; set; }

        internal static ExtractionOptions Resolve(ExtractionOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            if (options.SelfTest)
                return options;

            string aoClientPath = options.AoClientPath;
            if (string.IsNullOrWhiteSpace(aoClientPath))
                aoClientPath = Environment.GetEnvironmentVariable("AO_CLIENT_PATH");

            if (string.IsNullOrWhiteSpace(aoClientPath))
            {
                throw new ArgumentException(
                    "--ao-path is required unless AO_CLIENT_PATH is set.");
            }

            string repoRoot = RepositoryRootResolver.Resolve();
            string gameDataDirectory = options.GameDataDirectory;
            if (string.IsNullOrWhiteSpace(gameDataDirectory))
            {
                gameDataDirectory = Path.Combine(
                    repoRoot,
                    "AORebirth",
                    GameDataPaths.RootFolderName);
            }

            string outputDirectory = options.OutputDirectory;
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.Combine(
                    gameDataDirectory,
                    GameDataPaths.PlayfieldsFolderName);
            }

            return new ExtractionOptions
            {
                AoClientPath = Path.GetFullPath(aoClientPath.Trim()),
                OutputDirectory = Path.GetFullPath(outputDirectory.Trim()),
                GameDataDirectory = Path.GetFullPath(gameDataDirectory.Trim()),
                TilemapId = options.TilemapId,
                Overwrite = options.Overwrite,
                SelfTest = false,
                SkipMonsterData = options.SkipMonsterData,
            };
        }
    }
}
