namespace ZoneEngine_New.Core.NpcScripts;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

using ZoneEngine_New.Core.Ai;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Logging;

using Vector3 = AORebirth.Core.Vector.Vector3;

/// <summary>
/// The stable, readable identity of a <c>Spawns.json</c> point: <c>HASH@x,y,z</c> with the position rounded to
/// 0.1 m, plus <c>#n</c> for the n-th point stacked at the same place (RDB order). Derived at load from fields the
/// file already has; nothing is written to <c>Spawns.json</c>.
/// </summary>
public static class SpawnPointKey
{
    public static string Build(string hashText, float x, float y, float z, int ordinal)
    {
        string key = string.Create(CultureInfo.InvariantCulture, $"{hashText}@{x:F1},{y:F1},{z:F1}");
        return ordinal > 1 ? key + "#" + ordinal.ToString(CultureInfo.InvariantCulture) : key;
    }

    /// <summary>Re-formats an authored key so spacing and number formatting cannot make it miss; empty when malformed.</summary>
    public static string Normalize(string text)
    {
        string trimmed = text.Trim();
        int at = trimmed.IndexOf('@');
        if (at <= 0)
            return string.Empty;
        string hash = trimmed.Substring(0, at).Trim();
        string rest = trimmed.Substring(at + 1);
        int ordinal = 1;
        int hashMark = rest.IndexOf('#');
        if (hashMark >= 0)
        {
            if (!int.TryParse(rest.AsSpan(hashMark + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out ordinal) || ordinal < 1)
                return string.Empty;
            rest = rest.Substring(0, hashMark);
        }

        string[] parts = rest.Split(',');
        if (parts.Length != 3 || hash.Length == 0)
            return string.Empty;
        var values = new float[3];
        for (int i = 0; i < 3; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                return string.Empty;
        }

        return Build(hash, values[0], values[1], values[2], ordinal);
    }
}

/// <summary>
/// <c>GameData/NpcScripts/*.json</c>: programs and their bindings, compiled at load and indexed by hash, by
/// (playfield, spawn point key) and by (playfield, NPC key). A program or binding with a problem is logged and left
/// out; the rest load. Resolution at spawn, most specific first: spawn point, NPC key, hash on this playfield, hash.
/// A file in the GameData override folder (AO_REBIRTH_GAMEDATA_OVERRIDE_PATH/NpcScripts) replaces the shipped file at
/// the same relative path, and one only there is added. <see cref="Reload"/> compiles everything again and swaps it in
/// whole; NPCs already running a program keep it until they respawn.
/// </summary>
public sealed class NpcScriptCatalog
{
    public const string DirectoryName = "NpcScripts";
    public const string BindingsSuffix = ".bindings.json";

    static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>The name <see cref="Reload"/> is registered under.</summary>
    public const string ReloadName = "NpcScripts";

    readonly IZoneLogger _logger;
    readonly IGameData _gameData;
    readonly System.Threading.Lock _reloadLock = new();
    volatile Snapshot _current;

    public NpcScriptCatalog(IGameData gameData, IZoneLogger logger)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _gameData = gameData;
        Root = Path.Combine(gameData.RootPath, DirectoryName);
        _current = Build();
        LogLoaded(_current.Result);
    }

    public string Root { get; }

    public NpcScriptLoadResult Result => _current.Result;

    public int BindingCount => _current.BindingCount;

    public bool HasBindings => BindingCount > 0;

    public IEnumerable<NpcScriptProgram> Programs => _current.Programs.Values;

    /// <summary>
    /// Compiles every script again (through the override folder) and swaps the result in. A file with a problem is left
    /// out and reported, as at startup; a folder that cannot be read keeps the current scripts. Off the playfield tick.
    /// </summary>
    public AORebirth.Modules.Content.GameDataReloadResult Reload()
    {
        lock (_reloadLock)
        {
            Snapshot next;
            try
            {
                next = Build();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new(ReloadName, false, _current.Result.Programs, [DirectoryName + ": " + exception.Message]);
            }

            _current = next;
            LogLoaded(next.Result);
            return new(ReloadName, true, next.Result.Programs, next.Result.Problems);
        }
    }

    void LogLoaded(NpcScriptLoadResult result)
    {
        foreach (string problem in result.Problems)
            _logger.Warn("NpcScripts: " + problem);
        _logger.Info(string.Format(
            CultureInfo.InvariantCulture,
            "NpcScripts loaded programs={0} bindings={1} problems={2} from {3}",
            result.Programs,
            result.Bindings,
            result.Problems.Count,
            Root));
    }

    /// <summary>Every script file by its '/'-separated path under NpcScripts: the override folder's copy wins.</summary>
    List<(string Name, string Path)> ScriptFiles()
    {
        var byName = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddFolder(byName, Root);
        string? overrideRoot = AORebirth.Core.GameData.GameDataPaths.ResolveOverrideRoot();
        if (overrideRoot != null)
            AddFolder(byName, Path.Combine(overrideRoot, DirectoryName));

        var files = new List<(string Name, string Path)>(byName.Count);
        foreach (KeyValuePair<string, string> pair in byName)
            files.Add((pair.Key, pair.Value));
        return files;
    }

    static void AddFolder(SortedDictionary<string, string> byName, string folder)
    {
        if (!Directory.Exists(folder))
            return;
        foreach (string file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
            byName[Path.GetRelativePath(folder, file).Replace('\\', '/')] = file;
    }

    Snapshot Build()
    {
        var snapshot = new Snapshot();
        snapshot.Result = Load(snapshot);
        return snapshot;
    }

    NpcScriptLoadResult Load(Snapshot snapshot)
    {
        var problems = new List<string>();
        List<(string Name, string Path)> files = ScriptFiles();
        if (files.Count == 0)
            return new NpcScriptLoadResult(0, 0, problems);

        var compiler = new NpcScriptCompiler(_gameData);

        // Programs first, so a sibling .bindings.json can name any of them.
        var bindingFiles = new List<(string File, JsonDocument Document)>();
        var documents = new List<JsonDocument>();
        foreach ((string name, string file) in files)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(file), JsonOptions);
            }
            catch (JsonException ex)
            {
                problems.Add(name + ": " + ex.Message);
                continue;
            }

            documents.Add(document);
            if (file.EndsWith(BindingsSuffix, StringComparison.OrdinalIgnoreCase))
            {
                bindingFiles.Add((name, document));
                continue;
            }

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                problems.Add(name + ": root must be an object");
                continue;
            }

            NpcScriptProgram? program = compiler.CompileProgram(name, root, problems);
            if (program == null)
                continue;
            if (!snapshot.Programs.TryAdd(program.Id, program))
            {
                problems.Add(name + ": program id '" + program.Id + "' is already defined in " + snapshot.Programs[program.Id].File);
                continue;
            }

            if (root.TryGetProperty("Bindings", out JsonElement bindings))
                AddBindings(snapshot, name, bindings, program, compiler, problems);
        }

        foreach ((string file, JsonDocument document) in bindingFiles)
        {
            JsonElement root = document.RootElement;
            NpcScriptProgram? program = null;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Script", out JsonElement scriptId) && scriptId.ValueKind == JsonValueKind.String)
            {
                if (!snapshot.Programs.TryGetValue(scriptId.GetString()!.Trim(), out program))
                {
                    problems.Add(file + ": unknown Script '" + scriptId.GetString() + "'");
                    continue;
                }
            }

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Bindings", out JsonElement bindings))
            {
                problems.Add(file + ": needs Bindings (and Script unless every binding is movement-only)");
                continue;
            }

            AddBindings(snapshot, file, bindings, program, compiler, problems);
        }

        foreach (JsonDocument document in documents)
            document.Dispose();

        foreach (NpcScriptProgram program in snapshot.Programs.Values)
        {
            if (!IsBound(snapshot, program))
                _logger.Info("NpcScripts: program '" + program.Id + "' (" + program.File + ") has no bindings");
        }

        return new NpcScriptLoadResult(snapshot.Programs.Count, snapshot.BindingCount, problems);
    }

    static bool IsBound(Snapshot snapshot, NpcScriptProgram program)
    {
        foreach (NpcScriptBinding binding in snapshot.ByHash.Values)
            if (ReferenceEquals(binding.Program, program)) return true;
        foreach (NpcScriptBinding binding in snapshot.ByHashOnPlayfield.Values)
            if (ReferenceEquals(binding.Program, program)) return true;
        foreach (NpcScriptBinding binding in snapshot.BySpawnPoint.Values)
            if (ReferenceEquals(binding.Program, program)) return true;
        foreach (NpcScriptBinding binding in snapshot.ByNpcKey.Values)
            if (ReferenceEquals(binding.Program, program)) return true;
        return false;
    }

    static void AddBindings(Snapshot snapshot, string file, JsonElement bindings, NpcScriptProgram? program, NpcScriptCompiler compiler, List<string> problems)
    {
        if (bindings.ValueKind != JsonValueKind.Array)
        {
            problems.Add(file + ": Bindings must be a list");
            return;
        }

        int index = 0;
        foreach (JsonElement element in bindings.EnumerateArray())
        {
            NpcScriptBinding? binding = compiler.CompileBinding(file, index++, element, program, problems);
            if (binding == null)
                continue;

            bool added;
            string where;
            switch (binding.Target)
            {
                case ScriptBindingTarget.SpawnPoint:
                    added = snapshot.BySpawnPoint.TryAdd((binding.PlayfieldId!.Value, binding.Key), binding);
                    where = "spawn point " + binding.Key + " on playfield " + binding.PlayfieldId;
                    break;
                case ScriptBindingTarget.NpcKey:
                    added = snapshot.ByNpcKey.TryAdd((binding.PlayfieldId!.Value, binding.Key), binding);
                    where = "NPC " + binding.Key + " on playfield " + binding.PlayfieldId;
                    break;
                default:
                    if (binding.PlayfieldId is int pf)
                    {
                        added = snapshot.ByHashOnPlayfield.TryAdd((pf, binding.Key), binding);
                        where = "hash " + binding.Key + " on playfield " + pf;
                    }
                    else
                    {
                        added = snapshot.ByHash.TryAdd(binding.Key, binding);
                        where = "hash " + binding.Key;
                    }

                    break;
            }

            if (!added)
                problems.Add(file + " binding " + (index - 1) + ": " + where + " is already bound");
        }
    }

    /// <summary>The most specific binding for an NPC, or null.</summary>
    public NpcScriptBinding? Resolve(string? hash, int playfieldId, string? spawnPointKey, string? npcKey)
    {
        Snapshot current = _current;
        NpcScriptBinding? binding;
        if (spawnPointKey != null && current.BySpawnPoint.TryGetValue((playfieldId, spawnPointKey), out binding))
            return binding;
        if (npcKey != null && current.ByNpcKey.TryGetValue((playfieldId, npcKey), out binding))
            return binding;
        if (hash == null)
            return null;
        if (current.ByHashOnPlayfield.TryGetValue((playfieldId, hash), out binding))
            return binding;
        return current.ByHash.TryGetValue(hash, out binding) ? binding : null;
    }

    /// <summary>
    /// True when a spawn point of <paramref name="hash"/> binds a script program (not plain patrol or wander). Resolves
    /// exactly as <see cref="TryAttach"/> does for a hash spawn, which passes the point's hash as the NPC's SpawnHash.
    /// </summary>
    public bool BindsProgram(string? hash, int playfieldId, string? spawnPointKey)
        => BindingCount > 0 && Resolve(hash, playfieldId, spawnPointKey, npcKey: null)?.Program != null;

    /// <summary>
    /// Resolves and applies the binding for a freshly built NPC: plain movement onto its brain, the program as a
    /// runner, then <see cref="NpcScriptTrigger.Spawned"/>. Returns false when nothing is bound.
    /// </summary>
    public bool TryAttach(NpcCharacter npc, int playfieldId, string? spawnPointKey, string? npcKey)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (BindingCount == 0)
            return false;

        NpcScriptBinding? binding = Resolve(npc.SpawnHash ?? npc.MobTemplate?.Hash, playfieldId, spawnPointKey, npcKey);
        if (binding == null)
            return false;

        // A NoCombat NPC spawns without a brain, and scripted walks (and plain patrol / wander) move through one. Such an
        // NPC gets a brain for movement only: a brain never aggros or fights for an NPC that is not attackable
        // (NpcBrain.CanAggro / CombatRules.CanAttack), so the Borealis Careless Citizen walks instead of emoting in place.
        NpcBrain? brain = npc.Brain;
        if (brain == null && NeedsMovement(binding))
            brain = NpcBrain.Create(npc, npc.Position, NpcAiProfiles.Resolve(npc.SpawnHash ?? npc.MobTemplate?.Hash));

        if (brain != null)
        {
            if (binding.Patrol != null)
            {
                brain.PatrolWaypoints = binding.Patrol.Waypoints;
                brain.PatrolRun = binding.Patrol.Run;
                brain.PatrolEnabled = true;
            }
            else if (binding.Wander != null)
            {
                Vector3 anchor = brain.Home ?? npc.Position;
                brain.EnableWander(anchor, binding.Wander.Radius, binding.Wander.MinPauseSeconds, binding.Wander.MaxPauseSeconds);
            }
        }

        if (binding.Program != null)
        {
            var runner = new NpcScriptRunner(binding.Program, binding.Args, npc);
            npc.AttachScript(runner);
            runner.Raise(NpcScriptTrigger.Spawned);
        }

        return true;
    }

    /// <summary>True when the binding moves its NPC: plain patrol or wander, or a program with a walk.</summary>
    static bool NeedsMovement(NpcScriptBinding binding)
        => binding.Patrol != null
           || binding.Wander != null
           || (binding.Program != null && Array.Exists(binding.Program.Ops, op => op.Code == ScriptOpCode.MoveTo));

    /// <summary>Logs every spawn point binding of a playfield whose key matched none of the playfield's points.</summary>
    public void ReportUnmatchedSpawnPoints(int playfieldId, IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Dictionary<(int Playfield, string Key), NpcScriptBinding> bySpawnPoint = _current.BySpawnPoint;
        if (bySpawnPoint.Count == 0)
            return;

        var known = new HashSet<string>(keys, StringComparer.Ordinal);
        foreach (KeyValuePair<(int Playfield, string Key), NpcScriptBinding> pair in bySpawnPoint)
        {
            if (pair.Key.Playfield == playfieldId && !known.Contains(pair.Key.Key))
                _logger.Warn("NpcScripts: binding " + pair.Value.Describe() + " matches no spawn point (" + pair.Value.Program?.File + ")");
        }
    }

    /// <summary>One load's programs and bindings; never changed after <see cref="Build"/>.</summary>
    sealed class Snapshot
    {
        public readonly Dictionary<string, NpcScriptProgram> Programs = new(StringComparer.Ordinal);
        public readonly Dictionary<string, NpcScriptBinding> ByHash = new(StringComparer.Ordinal);
        public readonly Dictionary<(int Playfield, string Key), NpcScriptBinding> ByHashOnPlayfield = new();
        public readonly Dictionary<(int Playfield, string Key), NpcScriptBinding> BySpawnPoint = new();
        public readonly Dictionary<(int Playfield, string Key), NpcScriptBinding> ByNpcKey = new();

        public NpcScriptLoadResult Result { get; set; } = new(0, 0, []);

        public int BindingCount => ByHash.Count + ByHashOnPlayfield.Count + BySpawnPoint.Count + ByNpcKey.Count;
    }
}
