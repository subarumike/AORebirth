using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using EasyHook;

namespace EasyHookSmokeInjector
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string title = GetArgument(args, "--title") ?? "Youwillmezz";
            string processName = GetArgument(args, "--process") ?? "AnarchyOnline";
            string pidArg = GetArgument(args, "--pid");
            string payloadPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\EasyHookSmokePayload\bin\Debug\EasyHookSmokePayload.dll"));
            string logPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-target.log"));

            Process target = !string.IsNullOrWhiteSpace(pidArg)
                ? Process.GetProcessById(int.Parse(pidArg))
                : Process.GetProcessesByName(processName)
                    .Where(process => string.IsNullOrWhiteSpace(title) || process.MainWindowTitle.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Single();

            Console.WriteLine("target pid=" + target.Id + " title=" + target.MainWindowTitle);
            Console.WriteLine("payload=" + payloadPath);
            Console.WriteLine("log=" + logPath);

            RemoteHooking.Inject(
                target.Id,
                InjectionOptions.DoNotRequireStrongName,
                payloadPath,
                payloadPath,
                logPath);

            Console.WriteLine("injected");
            return 0;
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
    }
}
