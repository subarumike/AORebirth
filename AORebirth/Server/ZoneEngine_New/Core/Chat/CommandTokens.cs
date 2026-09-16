namespace ZoneEngine_New.Core.Chat;

using System;

/// <summary>Tokenizes N3 chat command text; authorization remains with the command handler.</summary>
internal static class CommandTokens
{
    internal static string[] Read(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 4096) return [];
        int end = text.IndexOf('\0');
        if (end >= 0) text = text[..end];
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];
        words[0] = words[0].TrimStart('/', '.');
        if (words[0].Equals("command", StringComparison.OrdinalIgnoreCase)) words = words[1..];
        return words;
    }
}
