namespace ZoneEngine_New.Core.Knubot;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>One GameData/Knubot/*.json script as authored.</summary>
public sealed class KnubotScriptFile
{
    /// <summary>NPCs this script talks for: content NPC identities, placement keys, spawn hashes or template hashes.</summary>
    public string[] Npcs { get; set; } = [];

    /// <summary>Tried in order when the chat opens; the first whose conditions all pass is said.</summary>
    public KnubotOpenerFile[] Openers { get; set; } = [];

    /// <summary>Every reply the player can pick, by id; lines offer them by id so one reply is written once.</summary>
    public Dictionary<string, KnubotReplyFile> Replies { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, KnubotLineFile> Lines { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// What the NPC takes in its Give Item window, tried in file order when the player accepts. Opened by a line's
    /// <c>{opentrade}</c> or by the player directly.
    /// </summary>
    public Dictionary<string, KnubotTradeFile> Trades { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// One trade: the window must hold exactly <see cref="Items"/> (by stack count) and the player must offer exactly
/// <see cref="Credits"/>. Conditions work as on replies.
/// </summary>
public sealed class KnubotTradeFile
{
    public string[] If { get; set; } = [];
    public KnubotRequirementFile[] Requirements { get; set; } = [];
    public KnubotTradeItemFile[] Items { get; set; } = [];
    public int Credits { get; set; }

    /// <summary>A line id, <c>close</c>, or <c>opener</c>, as on a reply.</summary>
    public string Goto { get; set; } = string.Empty;
}

public sealed class KnubotTradeItemFile
{
    /// <summary>Item hash; any QL minted from it matches.</summary>
    public string Hash { get; set; } = string.Empty;
    public int Count { get; set; } = 1;
}

public sealed class KnubotOpenerFile
{
    public string[] If { get; set; } = [];

    /// <summary>At least one must pass (as well as every <see cref="If"/>). Empty: no extra check.</summary>
    public string[] Any { get; set; } = [];

    public KnubotRequirementFile[] Requirements { get; set; } = [];

    /// <summary>Line to open the chat with. Set this or <see cref="Vicinity"/>.</summary>
    public string? Say { get; set; }

    /// <summary>Said aloud by the NPC to players nearby instead of opening a chat.</summary>
    public string? Vicinity { get; set; }
}

public sealed class KnubotLineFile
{
    /// <summary>What the NPC says, with inline tags: one string, or an array of lines joined with line breaks.</summary>
    [JsonConverter(typeof(KnubotLinesConverter))]
    public string Text { get; set; } = string.Empty;

    /// <summary>Line said instead when this line's effects cannot be applied.</summary>
    public string? Fail { get; set; }

    /// <summary>Ids from the script's <see cref="KnubotScriptFile.Replies"/>, offered in this order.</summary>
    public string[] Replies { get; set; } = [];
}

/// <summary>Reads a string, or an array of strings joined with \n.</summary>
public sealed class KnubotLinesConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString() ?? string.Empty;

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Text must be a string or an array of strings.");

        var lines = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Text array entries must be strings.");
            lines.Add(reader.GetString() ?? string.Empty);
        }

        return string.Join("\n", lines);
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}

public sealed class KnubotReplyFile
{
    public string Text { get; set; } = string.Empty;
    public string[] If { get; set; } = [];
    public KnubotRequirementFile[] Requirements { get; set; } = [];

    /// <summary>A line id, <c>close</c>, or <c>opener</c> (pick the opener again). Empty or unknown: a dead end.</summary>
    public string Goto { get; set; } = string.Empty;
}

/// <summary>
/// One criteria row, in the same form and evaluated the same way as item requirements: a leaf compares
/// <see cref="Stat"/> to <see cref="Value"/> with <see cref="Operator"/> (EqualTo, Unequal, LessThan, GreaterThan,
/// BitAnd, NotBitAnd); an And, Or or Not row (no Stat) links the rows before it in postfix order. Rows without
/// links must all pass. Stat and Operator are names or numbers.
/// </summary>
public sealed class KnubotRequirementFile
{
    public string Stat { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public int Value { get; set; }
}

/// <summary>A validated, read-only script.</summary>
public sealed class KnubotScript(string id, string[] npcs, KnubotOpener[] openers, IReadOnlyDictionary<string, KnubotLine> lines,
    KnubotTrade[] trades)
{
    public const string CloseTarget = "close";
    public const string OpenerTarget = "opener";

    /// <summary>Added by the engine after every reply list, so scripts never write their own.</summary>
    public static readonly KnubotReply Goodbye = new("goodbye", [new KnubotTextPiece(KnubotTextKind.Literal, "Goodbye", Emote: false)], [], CloseTarget);

    public string Id { get; } = id;
    public string[] Npcs { get; } = npcs;
    public KnubotOpener[] Openers { get; } = openers;
    public IReadOnlyDictionary<string, KnubotLine> Lines { get; } = lines;
    public KnubotTrade[] Trades { get; } = trades;

    /// <summary>A reply with no content behind it yet: the engine says a debug line and offers the same replies again.</summary>
    public bool IsDeadEnd(KnubotReply reply)
        => reply.Goto is not (CloseTarget or OpenerTarget) && !Lines.ContainsKey(reply.Goto);
}

/// <summary>Exactly one of <see cref="Say"/> (a line id) and <see cref="Vicinity"/> (spoken text) is set.</summary>
public sealed record KnubotOpener(KnubotCondition[] If, KnubotCondition[] Any, string? Say, string? Vicinity)
{
    public bool Passes(KnubotContext context)
        => KnubotCondition.All(If, context) && (Any.Length == 0 || Array.Exists(Any, x => x.Passes(context)));
}

/// <summary><see cref="Id"/> is the reply's key in the script's Replies, for logs.</summary>
public sealed record KnubotReply(string Id, KnubotTextPiece[] Text, KnubotCondition[] If, string Goto);

/// <summary><see cref="Id"/> is the trade's key in the script's Trades, for logs.</summary>
public sealed record KnubotTrade(string Id, KnubotCondition[] If, KnubotTradeItem[] Items, int Credits, string Goto)
{
    /// <summary>
    /// True when <paramref name="offered"/> is exactly this trade: every item counts toward one wanted hash, every
    /// count is met, nothing is left over, and the credits are exact.
    /// </summary>
    public bool Matches(IEnumerable<ZoneEngine_New.Core.Inventory.Item> offered, int credits)
    {
        if (credits != Credits)
            return false;

        int[] missing = new int[Items.Length];
        for (int i = 0; i < Items.Length; i++)
            missing[i] = Items[i].Count;

        foreach (ZoneEngine_New.Core.Inventory.Item item in offered)
        {
            int count = Math.Max(1, item.StackCount);
            int wanted = Array.FindIndex(Items, x => x.Matches(item.LowId, item.HighId));
            while (wanted >= 0 && missing[wanted] < count)
                wanted = Array.FindIndex(Items, wanted + 1, x => x.Matches(item.LowId, item.HighId));

            if (wanted < 0)
                return false;

            missing[wanted] -= count;
        }

        return Array.TrueForAll(missing, x => x == 0);
    }
}

/// <summary><see cref="TemplateIds"/> is every AOID <see cref="Hash"/> can mint.</summary>
public sealed record KnubotTradeItem(string Hash, HashSet<int> TemplateIds, int Count)
{
    public bool Matches(int lowId, int highId) => TemplateIds.Contains(lowId) || TemplateIds.Contains(highId);
}

/// <summary><see cref="Effects"/> is every effect in <see cref="Pieces"/>, checked together before the line starts.</summary>
public sealed record KnubotLine(
    string Id,
    KnubotPiece[] Pieces,
    KnubotEffect[] Effects,
    string? Fail,
    KnubotReply[] Replies,
    string? Goto,
    int? CloseSeconds);

/// <summary>Text or pacing inside a line, in the order written.</summary>
public abstract record KnubotPiece;

public enum KnubotTextKind { Literal, PlayerName, NpcName }

/// <summary>Spoken text, or narration when <see cref="Emote"/> (AppendText kind 1).</summary>
public sealed record KnubotTextPiece(KnubotTextKind Kind, string Text, bool Emote) : KnubotPiece;

public sealed record KnubotDelayPiece(int Milliseconds) : KnubotPiece;

/// <summary>An effect applied when the text reaches it.</summary>
public sealed record KnubotEffectPiece(KnubotEffect Effect) : KnubotPiece;
