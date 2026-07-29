using System;
using System.Globalization;
using System.IO;

namespace ZoneEngine.Core.Missions;

internal static class MissionDiagnostics
{
	private static readonly object Gate = new object();

	private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "mission-diag.log");

	internal static void Log(string format, params object[] args)
	{
		string arg;
		try
		{
			arg = ((args != null && args.Length != 0) ? string.Format(CultureInfo.InvariantCulture, format, args) : format);
		}
		catch (FormatException)
		{
			arg = format;
		}
		string contents = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff} {1}{2}", DateTime.UtcNow, arg, Environment.NewLine);
		try
		{
			lock (Gate)
			{
				File.AppendAllText(LogPath, contents);
			}
		}
		catch
		{
		}
	}
}
