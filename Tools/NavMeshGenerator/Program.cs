using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using AORebirth.Core.GameData;
using AORebirth.World.Collision;
using AORebirth.World.Pathfinding;

internal static class Program
{
    static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            string gameData = string.IsNullOrWhiteSpace(options.GameDataRoot)
                ? ResolveRepoGameData()
                : options.GameDataRoot;
            if (!Directory.Exists(gameData))
                throw new DirectoryNotFoundException("GameData directory not found: " + gameData);
            Console.WriteLine("GAMEDATA " + gameData);

            string configPath = string.IsNullOrWhiteSpace(options.ConfigPath)
                ? NavMeshBuildSettings.DefaultPathBesideGameData(gameData)
                : options.ConfigPath;
            NavMeshBuildSettings settings = NavMeshBuildSettings.Load(configPath);
            Console.WriteLine("CONFIG " + configPath);

            var ids = new List<int>();
            if (options.PlayfieldId.HasValue)
            {
                ids.Add(options.PlayfieldId.Value);
            }
            else
            {
                string playfields = Path.Combine(gameData, GameDataPaths.PlayfieldsFolderName);
                if (!Directory.Exists(playfields))
                    throw new DirectoryNotFoundException("Playfields folder not found: " + playfields);

                string[] dirs = Directory.GetDirectories(playfields);
                for (int i = 0; i < dirs.Length; i++)
                {
                    if (int.TryParse(Path.GetFileName(dirs[i]), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                        && id > 0)
                        ids.Add(id);
                }

                ids.Sort();
            }

            int baked = 0;
            int skipped = 0;
            int failed = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (DungeonPlayfieldKinds.IsStyleTemplate(gameData, id))
                {
                    Console.WriteLine("SKIP template playfield=" + id.ToString(CultureInfo.InvariantCulture));
                    skipped++;
                    continue;
                }

                string output = Path.Combine(gameData, GameDataPaths.PlayfieldNavMeshRelativePath(id));
                if (File.Exists(output) && !options.Overwrite)
                {
                    Console.WriteLine("SKIP exists playfield=" + id.ToString(CultureInfo.InvariantCulture));
                    skipped++;
                    continue;
                }

                try
                {
                    var collision = PlayfieldCollisionResolver.Resolve(gameData, id);
                    if (!collision.HasCollision)
                    {
                        Console.WriteLine("SKIP empty-collision playfield=" + id.ToString(CultureInfo.InvariantCulture));
                        skipped++;
                        continue;
                    }

                    NavMeshBakeResult result = NavMeshBaker.Bake(collision, settings);
                    NavMeshFile.Write(output, result);
                    Console.WriteLine(
                        "BAKE playfield="
                        + id.ToString(CultureInfo.InvariantCulture)
                        + " triangles="
                        + result.SourceTriangles.ToString(CultureInfo.InvariantCulture)
                        + " tiles="
                        + result.TileCount.ToString(CultureInfo.InvariantCulture));
                    baked++;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        "FAIL playfield="
                        + id.ToString(CultureInfo.InvariantCulture)
                        + " "
                        + exception.Message);
                    failed++;
                }
            }

            Console.WriteLine(
                "NAVMESH_GENERATE baked="
                + baked.ToString(CultureInfo.InvariantCulture)
                + " skipped="
                + skipped.ToString(CultureInfo.InvariantCulture)
                + " failed="
                + failed.ToString(CultureInfo.InvariantCulture));
            if (failed > 0)
            {
                Console.Error.WriteLine("NAVMESH_GENERATE=FAIL");
                return 1;
            }

            Console.WriteLine("NAVMESH_GENERATE=PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("NAVMESH_GENERATE=FAIL " + exception.Message);
            return 1;
        }
    }

    static string ResolveRepoGameData()
    {
        string? dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            string gameData = Path.Combine(dir, "AORebirth", GameDataPaths.RootFolderName);
            if (File.Exists(Path.Combine(dir, "AGENTS.md")) && Directory.Exists(gameData))
                return Path.GetFullPath(gameData);

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not locate AORebirth/"
            + GameDataPaths.RootFolderName
            + " from "
            + Directory.GetCurrentDirectory()
            + ".");
    }
}

internal sealed class Options
{
    public string GameDataRoot { get; init; } = "";

    public string ConfigPath { get; init; } = "";

    public int? PlayfieldId { get; init; }

    public bool Overwrite { get; init; }

    public static Options Parse(string[] args)
    {
        string gameData = "";
        string configPath = "";
        int? playfield = null;
        bool overwrite = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--overwrite")
            {
                overwrite = true;
                continue;
            }

            if (i + 1 >= args.Length)
                throw new ArgumentException("Incomplete option " + args[i]);

            if (args[i] == "--game-data")
            {
                gameData = Path.GetFullPath(args[++i]);
                continue;
            }

            if (args[i] == "--config")
            {
                configPath = Path.GetFullPath(args[++i]);
                continue;
            }

            if (args[i] == "--playfield")
            {
                if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id <= 0)
                    throw new ArgumentException("Expected a positive --playfield id.");
                playfield = id;
                continue;
            }

            throw new ArgumentException("Unknown option " + args[i]);
        }

        return new Options
        {
            GameDataRoot = gameData,
            ConfigPath = configPath,
            PlayfieldId = playfield,
            Overwrite = overwrite
        };
    }
}
