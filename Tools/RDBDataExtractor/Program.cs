namespace AORebirth.Tools.RDBDataExtractor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    using AODB;
    using CommandLine;

    internal static class Program
    {
        private static int Main(string[] args)
        {
            // AODB reads RDB strings with Windows-1252; netcoreapp/net5+ need this provider.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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

                int tilemapWritten = 0;
                int tilemapSkipped = 0;
                int districtWritten = 0;
                int districtSkipped = 0;
                int failed = 0;

                using (var controller = new RdbController(resolved.AoClientPath))
                {
                    var tilemaps = new TilemapExporter(controller, resolved.OutputDirectory);
                    var districts = new DistrictExporter(controller, resolved.OutputDirectory);

                    IEnumerable<int> playfieldIds;
                    if (resolved.TilemapId.HasValue)
                    {
                        playfieldIds = new[] { resolved.TilemapId.Value };
                    }
                    else
                    {
                        playfieldIds = tilemaps.EnumerateTilemapIds()
                            .Union(districts.EnumerateDistrictIds())
                            .OrderBy(id => id);
                    }

                    foreach (int playfieldId in playfieldIds)
                    {
                        bool hasTilemap = tilemaps.TryHasTilemapRecord(playfieldId);
                        bool hasDistrict = districts.TryHasDistrictRecord(playfieldId);
                        if (!hasTilemap && !hasDistrict)
                        {
                            failed++;
                            Console.Error.WriteLine(
                                "FAIL playfield "
                                + playfieldId
                                + " has neither tilemap nor district records.");
                            if (resolved.TilemapId.HasValue)
                            {
                                return 1;
                            }

                            continue;
                        }

                        if (hasTilemap)
                        {
                            try
                            {
                                ExportFileCounts counts = tilemaps.Export(
                                    playfieldId,
                                    resolved.Overwrite);
                                tilemapWritten += counts.Written;
                                tilemapSkipped += counts.Skipped;
                                if (counts.Written > 0)
                                {
                                    Console.WriteLine(
                                        "exported tilemap "
                                        + playfieldId
                                        + " written="
                                        + counts.Written
                                        + " skipped="
                                        + counts.Skipped);
                                }
                            }
                            catch (Exception exception)
                            {
                                failed++;
                                Console.Error.WriteLine(
                                    "FAIL tilemap "
                                    + playfieldId
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

                        if (hasDistrict)
                        {
                            try
                            {
                                ExportFileCounts counts = districts.Export(
                                    playfieldId,
                                    resolved.Overwrite);
                                districtWritten += counts.Written;
                                districtSkipped += counts.Skipped;
                                if (counts.Written > 0)
                                {
                                    Console.WriteLine(
                                        "exported district "
                                        + playfieldId
                                        + " written="
                                        + counts.Written
                                        + " skipped="
                                        + counts.Skipped);
                                }
                            }
                            catch (Exception exception)
                            {
                                failed++;
                                Console.Error.WriteLine(
                                    "FAIL district "
                                    + playfieldId
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
                }

                Console.WriteLine(
                    "RDBDataExtractor complete tilemapWritten="
                    + tilemapWritten
                    + " tilemapSkipped="
                    + tilemapSkipped
                    + " districtWritten="
                    + districtWritten
                    + " districtSkipped="
                    + districtSkipped
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
