using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using AOSharp.Bootstrap.IPC;

using EasyHook;

namespace AOSharpLiveInjector
{
    internal static class Program
    {
        private const string ProcessName = "AnarchyOnline";

        private static int Main(string[] args)
        {
            string pluginPath = GetArgument(args, "--plugin")
                ?? Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"));
            string titleContains = GetArgument(args, "--title");
            string pidArg = GetArgument(args, "--pid");
            string logPath = GetArgument(args, "--log")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AOSharpLiveInjector.log");

            try
            {
                Log(logPath, "Starting injector.");
                Log(logPath, "Plugin: " + pluginPath);

                if (!File.Exists(pluginPath))
                {
                    throw new FileNotFoundException("Capture plugin was not found.", pluginPath);
                }

                Process target = !string.IsNullOrWhiteSpace(pidArg)
                    ? Process.GetProcessById(int.Parse(pidArg, CultureInfo.InvariantCulture))
                    : FindTargetProcess(titleContains);
                Log(logPath, string.Format(CultureInfo.InvariantCulture, "Target process: pid={0} title={1}", target.Id, target.MainWindowTitle));

                string bootstrapPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AOSharp.Bootstrap.dll");
                Log(logPath, "Bootstrap: " + bootstrapPath);

                RemoteHooking.Inject(
                    target.Id,
                    InjectionOptions.DoNotRequireStrongName,
                    bootstrapPath,
                    bootstrapPath,
                    target.Id.ToString(CultureInfo.InvariantCulture));

                IPCClient pipe = new IPCClient(target.Id.ToString(CultureInfo.InvariantCulture));
                pipe.Connect();
                pipe.Send(new LoadAssemblyMessage { Assemblies = new[] { pluginPath } });
                Log(logPath, "Capture plugin injected. Keeping pipe open.");

                pipe.OnDisconnected += client => Log(logPath, "IPC pipe disconnected; plugin should be unloaded.");

                while (!target.HasExited)
                {
                    Thread.Sleep(1000);
                    target.Refresh();
                }

                Log(logPath, "Target process exited.");
                return 0;
            }
            catch (Exception ex)
            {
                Log(logPath, "ERROR: " + ex);
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static Process FindTargetProcess(string titleContains)
        {
            Process[] candidates = Process.GetProcessesByName(ProcessName)
                .Where(process => !string.IsNullOrWhiteSpace(process.MainWindowTitle))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(titleContains))
            {
                candidates = candidates
                    .Where(process => process.MainWindowTitle.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();
            }

            if (candidates.Length == 0)
            {
                throw new InvalidOperationException("No running AnarchyOnline process matched.");
            }

            if (candidates.Length > 1)
            {
                throw new InvalidOperationException("More than one AnarchyOnline process matched. Use --title <window text>.");
            }

            return candidates[0];
        }

        private static string GetArgument(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void Log(string path, string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(
                path,
                string.Format(CultureInfo.InvariantCulture, "{0:o} {1}{2}", DateTime.UtcNow, message, Environment.NewLine));
        }
    }
}
