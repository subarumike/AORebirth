using System;

namespace ZoneEngine.Core;

internal static class ChatCommandText
{
	public static string Normalize(string commandText)
	{
		if (string.IsNullOrWhiteSpace(commandText))
		{
			return string.Empty;
		}
		string text = commandText.TrimEnd(default(char)).TrimStart('.').TrimStart('/');
		string text2;
		do
		{
			text2 = text;
			text = text.Replace("  ", " ");
		}
		while (text2 != text);
		text = text.Trim();
		if (text.StartsWith("command ", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring("command ".Length).TrimStart();
		}
		return text;
	}
}
