namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.IO;

    #endregion

    /// <summary>
    /// Lightweight append-only diagnostic log for the RK mission flow. The engines only write to their
    /// console windows, so this dedicated file lets us inspect exactly what happened during a manual test
    /// (mission accept, key grant, zone resync, instance entry) without needing the console output.
    ///
    /// Output file: &lt;engine working dir&gt;\mission-diag.log (normally AORebirth\Built\Debug).
    /// </summary>
    internal static class MissionDiagnostics
    {
        private static readonly object Gate = new object();

        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "mission-diag.log");

        internal static void Log(string format, params object[] args)
        {
            string message;
            try
            {
                message = args != null && args.Length > 0
                    ? string.Format(CultureInfo.InvariantCulture, format, args)
                    : format;
            }
            catch (FormatException)
            {
                message = format;
            }

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss.fff} {1}{2}",
                DateTime.UtcNow,
                message,
                Environment.NewLine);

            try
            {
                lock (Gate)
                {
                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
                // Diagnostics must never disrupt gameplay; swallow any IO error.
            }
        }
    }
}
