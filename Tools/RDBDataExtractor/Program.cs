namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using CommandLine;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<ExtractionOptions>(args)
                .MapResult(
                    Run,
                    errors => 2);
        }

        private static int Run(ExtractionOptions options)
        {
            try
            {
                if (options.SelfTest)
                {
                    return SelfTests.Run() ? 0 : 1;
                }

                ExtractionOptions resolved = ExtractionOptions.Resolve(options);

                if (!Directory.Exists(resolved.AoClientPath))
                {
                    Console.Error.WriteLine(
                        "FAIL AO client path was not found: " + resolved.AoClientPath);
                    return 2;
                }

                Directory.CreateDirectory(resolved.OutputDirectory);

                int exported = 0;
                int skipped = 0;
                int failed = 0;
                using (var exporter = new TilemapExporter(
                    resolved.AoClientPath,
                    resolved.OutputDirectory))
                {
                    IEnumerable<int> tilemapIds = resolved.TilemapId.HasValue
                        ? new[] { resolved.TilemapId.Value }
                        : exporter.EnumerateTilemapIds();

                    foreach (int tilemapId in tilemapIds)
                    {
                        if (resolved.SkipExisting && exporter.HasCompleteExport(tilemapId))
                        {
                            skipped++;
                            continue;
                        }

                        try
                        {
                            exporter.Export(tilemapId);
                            exported++;
                            Console.WriteLine("exported tilemap " + tilemapId);
                        }
                        catch (Exception exception)
                        {
                            failed++;
                            Console.Error.WriteLine(
                                "FAIL tilemap "
                                + tilemapId
                                + " "
                                + exception.GetType().Name
                                + ": "
                                + exception.Message);
                            if (resolved.TilemapId.HasValue)
                            {
                                return 1;
                            }
                        }
                    }
                }

                Console.WriteLine(
                    "RDBDataExtractor complete exported="
                    + exported
                    + " skipped="
                    + skipped
                    + " failed="
                    + failed);
                return failed > 0 ? 1 : 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "FAIL " + exception.GetType().Name + ": " + exception.Message);
                return 1;
            }
        }
    }
}
