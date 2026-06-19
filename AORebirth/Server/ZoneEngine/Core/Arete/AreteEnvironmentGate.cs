namespace ZoneEngine.Core.Arete
{
    #region Usings ...

    using System;

    #endregion

    public static class AreteEnvironmentGate
    {
        public static bool IsDefaultEnabled(string environmentVariableName)
        {
            return IsDefaultEnabledValue(Environment.GetEnvironmentVariable(environmentVariableName));
        }

        public static bool IsDefaultEnabledValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string normalized = value.Trim();
            if (string.Equals(normalized, "0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "off", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(normalized, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase);
        }
    }
}
