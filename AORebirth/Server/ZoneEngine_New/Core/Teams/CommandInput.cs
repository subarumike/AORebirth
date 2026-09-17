namespace ZoneEngine_New.Core.Teams;

using System;
using System.Text.RegularExpressions;

/// <summary>Tokenizes the existing N3 command envelope without rewriting argument text.</summary>
internal static class CommandInput
{
    // Marker order and ASCII argument separators are part of the existing input contract.
    static readonly Regex Markers = new(@"\A\.*/*", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    static readonly Regex Envelope = new(@"\A(?i:command) +\s*", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal static string[] Tokenize(string? input)
    {
        if (input == null) return [];
        string body = Markers.Replace(input.TrimEnd('\0'), string.Empty, 1).Trim();
        return Envelope.Replace(body, string.Empty, 1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
