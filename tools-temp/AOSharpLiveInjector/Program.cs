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
        private const int CaptureBootstrapReadyTimeoutMs = 5000;

        private static int Main(string[] args)
        {
            if (args.Any(arg => string.Equals(arg, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                return RunSelfTest();
            }

            string pluginPath = GetArgument(args, "--plugin")
                ?? Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\AOSharpLiveCapture\bin\Debug\AOSharpLiveCapture.dll"));
            string titleContains = GetArgument(args, "--title");
            string pidArg = GetArgument(args, "--pid");
            string logPath = GetArgument(args, "--log")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AOSharpLiveInjector.log");
            IPCClient pipe = null;

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
                string channelName = target.Id.ToString(CultureInfo.InvariantCulture)
                    + AOSharp.Bootstrap.Main.CaptureSafeChannelSuffix;
                Log(logPath, "Bootstrap mode: capture-safe; isolated capture chat commands enabled.");
                if (IsCaptureBootstrapActive(channelName))
                {
                    throw new InvalidOperationException(
                        "A capture-safe AOSharp bootstrap is already active in the target client; refusing duplicate injection.");
                }

                RemoteHooking.Inject(
                    target.Id,
                    InjectionOptions.DoNotRequireStrongName,
                    bootstrapPath,
                    bootstrapPath,
                    channelName);

                pipe = new IPCClient(channelName);
                pipe.Connect();
                pipe.Send(new LoadAssemblyMessage { Assemblies = new[] { pluginPath } });
                if (!WaitForCaptureBootstrapReady(channelName, CaptureBootstrapReadyTimeoutMs))
                {
                    throw new InvalidOperationException(
                        "Capture-safe Bootstrap did not confirm hook and plugin readiness.");
                }

                Log(logPath, "Capture plugin injected.");
                Thread.Sleep(3000);
                return 0;
            }
            catch (Exception ex)
            {
                Log(logPath, "ERROR: " + ex);
                Console.Error.WriteLine(ex);
                return 1;
            }
            finally
            {
                try
                {
                    pipe?.Disconnect();
                }
                catch
                {
                }
            }
        }

        private static int RunSelfTest()
        {
            string safeChannel = "1234" + AOSharp.Bootstrap.Main.CaptureSafeChannelSuffix;
            if (AOSharp.Bootstrap.Main.CaptureSafeContractVersion != 5
                || !AOSharp.Bootstrap.Main.IsCaptureSafeChannel(safeChannel)
                || AOSharp.Bootstrap.Main.IsCaptureSafeChannel("1234")
                || !AOSharp.Bootstrap.Main.ShouldInstallCaptureCommandHook(true, true)
                || AOSharp.Bootstrap.Main.ShouldInstallCaptureCommandHook(true, false)
                || AOSharp.Bootstrap.Main.ShouldInstallCaptureCommandHook(false, true)
                || !AOSharp.Bootstrap.Main.IsCaptureChatCommand("/aocap start")
                || !AOSharp.Bootstrap.Main.IsCaptureChatCommand("/AOCAP STOP")
                || !AOSharp.Bootstrap.Main.IsCaptureChatCommand("/aosmoke status")
                || !string.Equals(
                    AOSharp.Bootstrap.Main.NormalizeCaptureChatCommand(" /AOCAP STOP"),
                    "/aocap STOP",
                    StringComparison.Ordinal)
                || AOSharp.Bootstrap.Main.IsCaptureChatCommand("/aocapture start")
                || AOSharp.Bootstrap.Main.IsCaptureChatCommand("/aosmoker stop")
                || AOSharp.Bootstrap.Main.IsCaptureChatCommand("/aocap\tstop")
                || AOSharp.Bootstrap.Main.IsCaptureChatCommand("/assist")
                || AOSharp.Bootstrap.Main.IsCaptureChatCommand("aocap start")
                || !string.Equals(
                    AOSharp.Bootstrap.Main.TypedChatLogFileName,
                    "AOSharpLiveCapture.typed-chat.log",
                    StringComparison.Ordinal)
                || !string.Equals(
                    AOSharp.Bootstrap.Main.ChatSocketLogFileName,
                    "AOSharpLiveCapture.chat-socket.log",
                    StringComparison.Ordinal)
                || !string.Equals(
                    AOSharp.Bootstrap.Main.GetCaptureSafeSingletonName(safeChannel),
                    AOSharp.Bootstrap.Main.CaptureSafeSingletonNamePrefix + safeChannel,
                    StringComparison.Ordinal))
            {
                Console.Error.WriteLine("FAIL: capture-safe bootstrap channel contract is invalid.");
                return 1;
            }

            Console.WriteLine("PASS: capture-safe bootstrap provides fail-closed isolated capture chat commands without native GUI rewriting.");
            return 0;
        }

        private static bool IsCaptureBootstrapActive(string channelName)
        {
            try
            {
                using (EventWaitHandle singleton = EventWaitHandle.OpenExisting(
                    AOSharp.Bootstrap.Main.GetCaptureSafeSingletonName(channelName)))
                {
                    return true;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }

        private static bool WaitForCaptureBootstrapReady(string channelName, int timeoutMs)
        {
            try
            {
                using (EventWaitHandle ready = EventWaitHandle.OpenExisting(
                    AOSharp.Bootstrap.Main.GetCaptureSafeSingletonName(channelName)))
                {
                    return ready.WaitOne(timeoutMs);
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
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
