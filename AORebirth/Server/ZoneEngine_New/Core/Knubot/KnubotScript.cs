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
}

public sealed class KnubotOpenerFile
{
    public string[] If { get; set; } = [];
    public KnubotRequirementFile[] Requirements { get; set; } = [];
    public string Say { get; set; } = string.Empty;
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
public sealed class KnubotScript(string id, string[] npcs, KnubotOpener[] openers, IReadOnlyDictionary<string, KnubotLine> lines)
{
    public const string CloseTarget = "close";
    public const string OpenerTarget = "opener";

    /// <summary>Added by the engine after every reply list, so scripts never write their own.</summary>
    public static readonly KnubotReply Goodbye = new("goodbye", [new KnubotTextPiece(KnubotTextKind.Literal, "Goodbye", Emote: false)], [], CloseTarget);

    public string Id { get; } = id;
    public string[] Npcs { get; } = npcs;
    public KnubotOpener[] Openers { get; } = openers;
    public IReadOnlyDictionary<string, KnubotLine> Lines { get; } = lines;

    /// <summary>A reply with no content behind it yet: the engine says a debug line and offers the same replies again.</summary>
    public bool IsDeadEnd(KnubotReply reply)
        => reply.Goto is not (CloseTarget or OpenerTarget) && !Lines.ContainsKey(reply.Goto);
}

public sealed record KnubotOpener(KnubotCondition[] If, string Say);

/// <summary><see cref="Id"/> is the reply's key in the script's Replies, for logs.</summary>
public sealed record KnubotReply(string Id, KnubotTextPiece[] Text, KnubotCondition[] If, string Goto);

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
