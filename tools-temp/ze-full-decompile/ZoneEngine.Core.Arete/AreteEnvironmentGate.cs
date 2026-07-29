using System;

namespace ZoneEngine.Core.Arete;

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
		string a = value.Trim();
		if (string.Equals(a, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "off", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return string.Equals(a, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "on", StringComparison.OrdinalIgnoreCase);
	}
}
