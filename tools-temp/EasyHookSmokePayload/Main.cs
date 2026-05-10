using System;
using System.Globalization;
using System.IO;

using EasyHook;

namespace EasyHookSmokePayload
{
    [Serializable]
    public sealed class Main : IEntryPoint
    {
        private readonly string _logPath;

        public Main(RemoteHooking.IContext context, string logPath)
        {
            _logPath = logPath;
            Log("ctor host=" + context.HostPID);
        }

        public void Run(RemoteHooking.IContext context, string logPath)
        {
            Log("run host=" + context.HostPID);
        }

        private void Log(string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath));
            File.AppendAllText(
                _logPath,
                string.Format(CultureInfo.InvariantCulture, "{0:o} {1}{2}", DateTime.UtcNow, message, Environment.NewLine));
        }
    }
}
