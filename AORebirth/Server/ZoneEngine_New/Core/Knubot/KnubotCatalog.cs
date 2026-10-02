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
/// </summary>
public sealed class KnubotCatalog
{
    public const string DirectoryName = "Knubot";

    /// <summary>Automatic transitions (goto and fail) one player action may take before the chat is closed.</summary>
    public const int MaxChain = 8;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    readonly Dictionary<string, KnubotScript> _byNpc = new(StringComparer.Ordinal);

    public KnubotCatalog(IGameData gameData, QuestCatalog quests, IItemTemplateCatalog items, IZoneLogger logger)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(logger);

        string root = Path.Combine(gameData.RootPath, DirectoryName);
        if (!Directory.Exists(root))
        {
            logger.Info("No Knubot scripts at " + root);
            return;
        }

        var load = new KnubotLoadContext(quests, gameData, items);
        int loaded = 0, rejected = 0;
        foreach (string path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            string id = Path.ChangeExtension(Path.GetRelativePath(root, path), null).Replace('\\', '/');
            var errors = new List<string>();
            KnubotScript? script = null;
            try
            {
                KnubotScriptFile? file = JsonSerializer.Deserialize<KnubotScriptFile>(File.ReadAllText(path), JsonOptions);
                script = file == null ? null : Compile(id, file, load, errors);
                if (file == null)
                    errors.Add("empty file");
            }
            catch (JsonException exception)
            {
                errors.Add(exception.Message);
            }

            if (script != null)
            {
                foreach (string npc in script.Npcs)
                {
                    if (_byNpc.TryGetValue(npc, out KnubotScript? owner))
                        errors.Add("NPC " + npc + " already talks through " + owner.Id);
                }
            }

            if (script == null || errors.Count != 0)
            {
                rejected++;
                logger.Error(string.Format(CultureInfo.InvariantCulture, "Knubot script {0} not loaded: {1}", path, string.Join("; ", errors)));
                continue;
            }

            foreach (string npc in script.Npcs)
                _byNpc[npc] = script;
            loaded++;

            var deadEnds = script.Lines.Values.SelectMany(line => line.Replies.Where(script.IsDeadEnd)
                .Select(reply => "line '" + line.Id + "' reply '" + reply.Id + "'")).ToArray();
            if (deadEnds.Length != 0)
                logger.Warn(string.Format(CultureInfo.InvariantCulture, "Knubot script {0} has {1} dead-end replies: {2}",
                    script.Id, deadEnds.Length, string.Join(", ", deadEnds)));
        }

        logger.Info(string.Format(CultureInfo.InvariantCulture, "Knubot scripts={0} rejected={1} npcs={2} from {3}", loaded, rejected, _byNpc.Count, root));
    }

    public int NpcCount => _byNpc.Count;

    /// <summary>
    /// The script for <paramref name="npc"/>: by its content NPC identity or placement key, then the hash it
    /// spawned from, then its template hash.
    /// </summary>
    public bool TryResolve(NpcCharacter npc, out KnubotScript script)
    {
        script = null!;
        if (_byNpc.Count == 0 || npc == null)
            return false;

        if (npc.Playfield?.GetRequiredService<NpcContentActivationService>().TryGetBinding(npc, out NpcContentBinding binding) == true
            && (Find(binding.ContentNpcIdentity, out script) || Find(binding.PlacementIdentity, out script)))
            return true;

        return Find(npc.SpawnHash, out script) || Find(npc.MobTemplate?.Hash, out script);
    }

    bool Find(string? key, out KnubotScript script)
    {
        script = null!;
        return !string.IsNullOrEmpty(key) && _byNpc.TryGetValue(key, out script!);
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

            trades.Add(new KnubotTrade(pair.Key, conditions, items.ToArray(), source.Credits, target));
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

        return new KnubotScript(id, npcs, openers.ToArray(), lines, trades.ToArray());
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
