namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.IO;

    using CommandLine;

    [Verb("extract", isDefault: true, HelpText = "Export RDB tilemaps to GameData/Playfields.")]
    internal sealed class ExtractionOptions
    {
        [Option(
            "ao-path",
            HelpText = "Path to the installed AO client. Falls back to AO_CLIENT_PATH when omitted.")]
        public string AoClientPath { get; set; }

        [Option(
            "output-dir",
            HelpText = "Output root. Defaults to AORebirth/GameData/Playfields under the repo root.")]
        public string OutputDirectory { get; set; }

        [Option("tilemap-id", HelpText = "Export one tilemap resource id.")]
        public int? TilemapId { get; set; }

        [Option(
            "skip-existing",
            HelpText = "Skip tilemaps that already have metadata and a height image.")]
        public bool SkipExisting { get; set; }

        [Option("self-test", HelpText = "Run built-in exporter self tests.")]
        public bool SelfTest { get; set; }

        internal static ExtractionOptions Resolve(ExtractionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.SelfTest)
            {
                return options;
            }

            string aoClientPath = options.AoClientPath;
            if (string.IsNullOrWhiteSpace(aoClientPath))
            {
                aoClientPath = Environment.GetEnvironmentVariable("AO_CLIENT_PATH");
            }

            if (string.IsNullOrWhiteSpace(aoClientPath))
            {
                throw new ArgumentException(
                    "--ao-path is required unless AO_CLIENT_PATH is set.");
            }

            string outputDirectory = options.OutputDirectory;
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.Combine(
                    RepositoryRootResolver.Resolve(),
                    "AORebirth",
                    "GameData",
                    "Playfields");
            }

            return new ExtractionOptions
            {
                AoClientPath = Path.GetFullPath(aoClientPath.Trim()),
                OutputDirectory = Path.GetFullPath(outputDirectory.Trim()),
                TilemapId = options.TilemapId,
                SkipExisting = options.SkipExisting,
                SelfTest = false,
            };
        }
    }
}
