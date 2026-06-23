namespace ZoneEngine.Core
{
    using System;

    internal static class ChatCommandText
    {
        public static string Normalize(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
            {
                return string.Empty;
            }

            string fullArgs = commandText.TrimEnd(char.MinValue).TrimStart('.').TrimStart('/');
            string previous;
            do
            {
                previous = fullArgs;
                fullArgs = fullArgs.Replace("  ", " ");
            }
            while (previous != fullArgs);

            fullArgs = fullArgs.Trim();
            const string commandPrefix = "command ";
            if (fullArgs.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                fullArgs = fullArgs.Substring(commandPrefix.Length).TrimStart();
            }

            return fullArgs;
        }
    }
}
