namespace ZoneEngine_New.Core.Knubot;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Logging;
using ZoneEngine_New.Core.Mobs;
using ZoneEngine_New.Core.Quests;

/// <summary>
/// GameData/Knubot/**/*.json scripts, validated at load and keyed by the NPCs they talk for. A script with any
/// error is logged with its file and line id and not loaded; the others still load.
/// <para>
/// <see cref="OverrideVariable"/> names a second folder laid over that one: a script there replaces the GameData
/// script with the same id (its path under the folder) and takes its NPCs from any other. <see cref="Reload"/> rebuilds
/// both while the zone runs; a chat already open keeps the script it started with.
/// </para>
/// </summary>
public sealed class KnubotCatalog
{
    public const string DirectoryName = "Knubot";

    /// <summary>Environment variable naming the override folder (e.g. where a development module uploads scripts).</summary>
    public const string OverrideVariable = "AO_REBIRTH_KNUBOT_PATH";

    /// <summary>Automatic transitions (goto and fail) one player action may take before the chat is closed.</summary>
    public const int MaxChain = 8;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    readonly string _root;
    readonly KnubotLoadContext _load;
    readonly IZoneLogger _logger;
    readonly object _reloadLock = new();

    // Swapped whole by Reload; a lookup reads the reference once.
    volatile Dictionary<string, KnubotScript> _byNpc;

    public KnubotCatalog(IGameData gameData, QuestCatalog quests, IItemTemplateCatalog items, IZoneLogger logger)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(logger);

        _root = Path.Combine(gameData.RootPath, DirectoryName);
        string? overrideRoot = Environment.GetEnvironmentVariable(OverrideVariable);
        OverrideRoot = string.IsNullOrWhiteSpace(overrideRoot) ? null : Path.GetFullPath(overrideRoot.Trim());
        _load = new KnubotLoadContext(quests, gameData, items);
        _logger = logger;
        _byNpc = Build().ByNpc;
    }

    /// <summary>The <see cref="OverrideVariable"/> folder, or null when it is not set.</summary>
    public string? OverrideRoot { get; }

    /// <summary>GameData/Knubot, where shipped scripts are read from.</summary>
    public string Root => _root;

    /// <summary>Every loaded script once (a script talks for several NPCs), by id.</summary>
    public IReadOnlyDictionary<string, KnubotScript> LoadedScripts()
    {
        var scripts = new SortedDictionary<string, KnubotScript>(StringComparer.Ordinal);
        foreach (KnubotScript script in _byNpc.Values)
            scripts[script.Id] = script;
        return scripts;
    }

    /// <summary>
    /// The file script <paramref name="id"/> is read from: the override folder's when it has one, else GameData's. Null
    /// when <paramref name="id"/> is not a plain relative script id or names no file.
    /// </summary>
    public string? FileFor(string id)
    {
        if (OverrideRoot != null && TryResolve(OverrideRoot, id, out string overridePath) && File.Exists(overridePath))
            return overridePath;
        return TryResolve(_root, id, out string path) && File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Where a script <paramref name="id"/> is written: over the file it is read from, else a new file under GameData.
    /// False for an id that is not a plain relative path of letters, digits, '_', '-' and '/' (no '..', no root).
    /// </summary>
    public bool TryWritePath(string id, out string path)
    {
        path = FileFor(id) ?? string.Empty;
        return path.Length != 0 || TryResolve(_root, id, out path);
    }

    static bool TryResolve(string root, string id, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id.StartsWith('/') || id.EndsWith('/') || id.Contains("//", StringComparison.Ordinal))
            return false;
        foreach (char c in id)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '/'))
                return false;
        }

        string full = Path.GetFullPath(Path.Combine(root, id.Replace('/', Path.DirectorySeparatorChar) + ".json"));
        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            return false;
        path = full;
        return true;
    }

    /// <summary>Replies that lead nowhere yet, as "line 'x' reply 'y'".</summary>
    public static string[] DeadEnds(KnubotScript script)
        => script.Lines.Values.SelectMany(line => line.Replies.Where(script.IsDeadEnd)
            .Select(reply => "line '" + line.Id + "' reply '" + reply.Id + "'")).ToArray();

    /// <summary>
    /// <see cref="Check"/> plus the dead-end replies the script would load with (empty when it would not load).
    /// Nothing is installed.
    /// </summary>
    public IReadOnlyList<string> Check(string id, string json, out string[] deadEnds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var errors = new List<string>();
        KnubotScript? script = Parse(id, json ?? string.Empty, errors);
        deadEnds = script != null && errors.Count == 0 ? DeadEnds(script) : [];
        return errors;
    }

    public int NpcCount => _byNpc.Count;

    /// <summary>
    /// Raised by <see cref="Reload"/> after the swap, with the NPC map before and after it, on the reloading thread.
    /// Handlers only queue work: spawned NPCs whose script presence changed are refreshed on their playfield ticks.
    /// </summary>
    public event Action<KnubotMapChange>? Reloaded;

    /// <summary>Loads both folders again and swaps the result in. Scripts that fail stay out, as at startup.</summary>
    public KnubotLoadResult Reload()
    {
        KnubotLoadResult result;
        Dictionary<string, KnubotScript> previous;
        lock (_reloadLock)
        {
            result = Build();
            previous = _byNpc;
            _byNpc = result.ByNpc;
        }

        try
        {
            Reloaded?.Invoke(new KnubotMapChange(previous, result.ByNpc));
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Knubot reload: refreshing spawned NPCs failed.");
        }

        return result;
    }

    /// <summary>
    /// The errors <paramref name="json"/> would be rejected with as script <paramref name="id"/>; empty when it would
    /// load. Nothing is installed.
    /// </summary>
    public IReadOnlyList<string> Check(string id, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var errors = new List<string>();
        Parse(id, json ?? string.Empty, errors);
        return errors;
    }

    KnubotScript? Parse(string id, string json, List<string> errors)
    {
        try
        {
            KnubotScriptFile? file = JsonSerializer.Deserialize<KnubotScriptFile>(json, JsonOptions);
            if (file == null)
            {
                errors.Add("empty file");
                return null;
            }

            return Compile(id, file, _load, errors);
        }
        catch (JsonException exception)
        {
            errors.Add(exception.Message);
            return null;
        }
    }

    KnubotLoadResult Build()
    {
        var byNpc = new Dictionary<string, KnubotScript>(StringComparer.Ordinal);
        var problems = new List<string>();
        if (!Directory.Exists(_root))
            _logger.Info("No Knubot scripts at " + _root);
        Dictionary<string, (KnubotScript Script, string Path)> shipped = LoadFolder(_root, problems);
        Dictionary<string, (KnubotScript Script, string Path)> overrides = OverrideRoot == null
            ? new(StringComparer.Ordinal)
            : LoadFolder(OverrideRoot, problems);

        // Overrides claim their NPCs first. A shipped script with the same id is replaced outright; one that shares an
        // NPC with an override gives that NPC up and keeps the rest.
        var claimedByOverride = new HashSet<string>(StringComparer.Ordinal);
        int loaded = 0;
        foreach ((string _, (KnubotScript script, string path)) in overrides.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (Install(script, path, byNpc, claimedByOverride, overriding: true, problems))
                loaded++;
        }

        foreach ((string id, (KnubotScript script, string path)) in shipped.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!overrides.ContainsKey(id) && Install(script, path, byNpc, claimedByOverride, overriding: false, problems))
                loaded++;
        }

        _logger.Info(string.Format(CultureInfo.InvariantCulture, "Knubot scripts={0} rejected={1} npcs={2} from {3}{4}",
            loaded, problems.Count, byNpc.Count, _root, OverrideRoot == null ? string.Empty : " with overrides from " + OverrideRoot));
        return new KnubotLoadResult(loaded, byNpc.Count, problems, byNpc);
    }

    /// <summary>Every script under <paramref name="root"/> that compiles, by id; each failure goes into <paramref name="problems"/>.</summary>
    Dictionary<string, (KnubotScript Script, string Path)> LoadFolder(string root, List<string> problems)
    {
        var scripts = new Dictionary<string, (KnubotScript, string)>(StringComparer.Ordinal);
        if (!Directory.Exists(root))
            return scripts;

        foreach (string path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            string id = Path.ChangeExtension(Path.GetRelativePath(root, path), null).Replace('\\', '/');
            var errors = new List<string>();
            KnubotScript? script = null;
            try
            {
                script = Parse(id, File.ReadAllText(path), errors);
            }
            catch (IOException exception)
            {
                // An upload being written right now; the next reload picks it up.
                errors.Add(exception.Message);
            }

            if (script == null || errors.Count != 0)
            {
                Reject(path, errors, problems);
                continue;
            }

            scripts[id] = (script, path);
        }

        return scripts;
    }

    bool Install(
        KnubotScript script,
        string path,
        Dictionary<string, KnubotScript> byNpc,
        HashSet<string> claimedByOverride,
        bool overriding,
        List<string> problems)
    {
        var errors = new List<string>();
        var npcs = new List<string>(script.Npcs.Length);
        foreach (string npc in script.Npcs)
        {
            if (!overriding && claimedByOverride.Contains(npc))
                continue;
            if (byNpc.TryGetValue(npc, out KnubotScript? owner))
                errors.Add("NPC " + npc + " already talks through " + owner.Id);
            else
                npcs.Add(npc);
        }

        if (errors.Count != 0)
        {
            Reject(path, errors, problems);
            return false;
        }

        foreach (string npc in npcs)
        {
            byNpc[npc] = script;
            if (overriding)
                claimedByOverride.Add(npc);
        }

        string[] deadEnds = DeadEnds(script);
        if (deadEnds.Length != 0)
            _logger.Warn(string.Format(CultureInfo.InvariantCulture, "Knubot script {0} has {1} dead-end replies: {2}",
                script.Id, deadEnds.Length, string.Join(", ", deadEnds)));
        return true;
    }

    void Reject(string path, List<string> errors, List<string> problems)
    {
        string detail = string.Join("; ", errors);
        problems.Add(path + ": " + detail);
        _logger.Error(string.Format(CultureInfo.InvariantCulture, "Knubot script {0} not loaded: {1}", path, detail));
    }

    /// <summary>
    /// The script for <paramref name="npc"/>: by its content NPC identity or placement key, then the hash it
    /// spawned from, then its template hash.
    /// </summary>
    public bool TryResolve(NpcCharacter npc, out KnubotScript script) => Resolve(_byNpc, npc, out script);

    /// <summary><see cref="TryResolve"/> against one NPC map (the live one, or one from a <see cref="KnubotMapChange"/>).</summary>
    public static bool Resolve(IReadOnlyDictionary<string, KnubotScript> byNpc, NpcCharacter npc, out KnubotScript script)
    {
        script = null!;
        if (byNpc.Count == 0 || npc == null)
            return false;

        if (npc.Playfield?.GetRequiredService<NpcContentActivationService>().TryGetBinding(npc, out NpcContentBinding binding) == true
            && (Find(byNpc, binding.ContentNpcIdentity, out script) || Find(byNpc, binding.PlacementIdentity, out script)))
            return true;

        return Find(byNpc, npc.SpawnHash, out script) || Find(byNpc, npc.MobTemplate?.Hash, out script);
    }

    static bool Find(IReadOnlyDictionary<string, KnubotScript> byNpc, string? key, out KnubotScript script)
    {
        script = null!;
        return !string.IsNullOrEmpty(key) && byNpc.TryGetValue(key, out script!);
    }

    static KnubotScript? Compile(string id, KnubotScriptFile file, KnubotLoadContext load, List<string> errors)
    {
        string[] npcs = (file.Npcs ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (npcs.Length == 0)
            errors.Add("Npcs is empty");
        if (file.Lines == null || file.Lines.Count == 0)
        {
            errors.Add("Lines is empty");
            return null;
        }

        var replyTable = new Dictionary<string, KnubotReply>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, KnubotReplyFile> pair in file.Replies ?? new Dictionary<string, KnubotReplyFile>())
        {
            KnubotReplyFile? reply = pair.Value;
            if (reply == null || string.IsNullOrWhiteSpace(reply.Text))
            {
                errors.Add("reply '" + pair.Key + "' needs Text");
                continue;
            }

            var replyErrors = new List<string>();
            KnubotParser.Parsed text = KnubotParser.Parse(reply.Text, reply: true, load, replyErrors);
            replyTable[pair.Key] = new KnubotReply(pair.Key, text.Pieces.OfType<KnubotTextPiece>().ToArray(),
                Conditions(reply.If, reply.Requirements, load, replyErrors), (reply.Goto ?? string.Empty).Trim());
            foreach (string error in replyErrors)
                errors.Add("reply '" + pair.Key + "': " + error);
        }

        var lines = new Dictionary<string, KnubotLine>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, KnubotLineFile> pair in file.Lines)
        {
            string lineId = pair.Key;
            KnubotLineFile source = pair.Value ?? new KnubotLineFile();
            if (lineId is KnubotScript.CloseTarget or KnubotScript.OpenerTarget)
                errors.Add("line id '" + lineId + "' is reserved");

            var lineErrors = new List<string>();
            KnubotParser.Parsed parsed = KnubotParser.Parse(source.Text, reply: false, load, lineErrors);
            if (parsed.Goto != null && (source.Replies?.Length ?? 0) != 0)
                lineErrors.Add("{goto} and Replies cannot both be set");
            if (parsed.Goto != null && parsed.CloseSeconds != null)
                lineErrors.Add("{goto} and {close} cannot both be set");

            var replies = new List<KnubotReply>();
            foreach (string replyId in source.Replies ?? [])
            {
                if (replyId != null && replyTable.TryGetValue(replyId.Trim(), out KnubotReply? reply))
                    replies.Add(reply);
                else
                    lineErrors.Add("unknown reply '" + replyId + "'");
            }

            foreach (string error in lineErrors)
                errors.Add("line '" + lineId + "': " + error);

            lines[lineId] = new KnubotLine(lineId, parsed.Pieces.ToArray(), parsed.Effects.ToArray(),
                string.IsNullOrWhiteSpace(source.Fail) ? null : source.Fail.Trim(), replies.ToArray(), parsed.Goto, parsed.CloseSeconds);
        }

        var openers = new List<KnubotOpener>();
        foreach (KnubotOpenerFile opener in file.Openers ?? [])
        {
            string? say = string.IsNullOrWhiteSpace(opener?.Say) ? null : opener!.Say!.Trim();
            string? vicinity = string.IsNullOrWhiteSpace(opener?.Vicinity) ? null : opener!.Vicinity!.Trim();
            if (opener == null || (say == null) == (vicinity == null))
            {
                errors.Add("opener needs exactly one of Say and Vicinity");
                continue;
            }

            if (say != null && !lines.ContainsKey(say))
            {
                errors.Add("opener says unknown line '" + say + "'");
                continue;
            }

            string name = say ?? "vicinity '" + vicinity + "'";
            var openerErrors = new List<string>();
            openers.Add(new KnubotOpener(Conditions(opener.If, opener.Requirements, load, openerErrors),
                Conditions(opener.Any, null, load, openerErrors), say, vicinity));
            foreach (string error in openerErrors)
                errors.Add("opener " + name + ": " + error);
        }

        if (openers.Count == 0)
            errors.Add("Openers is empty");

        var trades = new List<KnubotTrade>();
        foreach (KeyValuePair<string, KnubotTradeFile> pair in file.Trades ?? new Dictionary<string, KnubotTradeFile>())
        {
            KnubotTradeFile source = pair.Value ?? new KnubotTradeFile();
            var tradeErrors = new List<string>();
            var items = new List<KnubotTradeItem>();
            foreach (KnubotTradeItemFile item in source.Items ?? [])
            {
                string hash = (item?.Hash ?? string.Empty).Trim();
                if (item == null || item.Count <= 0 || !load.TryItemTemplates(hash, out HashSet<int> templates))
                    tradeErrors.Add("item needs a known Hash and a positive Count: '" + hash + "'");
                else
                    items.Add(new KnubotTradeItem(hash, templates, item.Count));
            }

            if (source.Credits < 0)
                tradeErrors.Add("Credits cannot be negative");
            if (items.Count == 0 && source.Credits == 0 && tradeErrors.Count == 0)
                tradeErrors.Add("needs Items or Credits");

            string target = (source.Goto ?? string.Empty).Trim();
            if (target is not (KnubotScript.CloseTarget or KnubotScript.OpenerTarget) && !lines.ContainsKey(target))
                tradeErrors.Add("Goto names unknown line '" + target + "'");

            KnubotCondition[] conditions = Conditions(source.If, source.Requirements, load, tradeErrors);
            foreach (string error in tradeErrors)
                errors.Add("trade '" + pair.Key + "': " + error);

            trades.Add(new KnubotTrade(pair.Key, conditions, items.ToArray(), source.Credits, target, source.ReturnItems));
        }

        foreach (KnubotLine line in lines.Values)
        {
            if (trades.Count == 0 && Array.Exists(line.Effects, x => x is KnubotOpenTrade))
                errors.Add("line '" + line.Id + "': {opentrade} needs a Trades section");
            if (line.Fail != null && !lines.ContainsKey(line.Fail))
                errors.Add("line '" + line.Id + "': Fail names unknown line '" + line.Fail + "'");
            if (line.Goto != null && !lines.ContainsKey(line.Goto))
                errors.Add("line '" + line.Id + "': {goto} names unknown line '" + line.Goto + "'");
        }

        string? tradeRefused = string.IsNullOrWhiteSpace(file.TradeRefused) ? null : file.TradeRefused.Trim();
        if (tradeRefused != null && !lines.ContainsKey(tradeRefused))
            errors.Add("TradeRefused names unknown line '" + tradeRefused + "'");

        return new KnubotScript(id, npcs, openers.ToArray(), lines, trades.ToArray(), tradeRefused);
    }

    static KnubotCondition[] Conditions(string[]? texts, KnubotRequirementFile[]? requirements, KnubotLoadContext load, List<string> errors)
    {
        var conditions = new List<KnubotCondition>();
        foreach (string text in texts ?? [])
        {
            if (KnubotCondition.TryParse(text, load, out KnubotCondition condition, out string error))
                conditions.Add(condition);
            else
                errors.Add(error);
        }

        if (KnubotCondition.TryParseRequirements(requirements, errors) is KnubotRequirementCondition rows)
            conditions.Add(rows);

        return conditions.ToArray();
    }
}
