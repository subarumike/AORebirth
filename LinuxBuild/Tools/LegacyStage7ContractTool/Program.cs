using System;
using System.IO;
using System.Reflection;

internal static class Program
{
    private static string assemblyDirectory;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3 || (args[0] != "write" && args[0] != "verify"))
            {
                Console.Error.WriteLine("Usage: LegacyStage7ContractTool <write|verify> <manifest-path> <legacy-LoginEngine.exe>");
                return 2;
            }

            string assemblyPath = Path.GetFullPath(args[2]);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("Legacy LoginEngine assembly was not found.", assemblyPath);
            }

            assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            AppDomain.CurrentDomain.AssemblyResolve += ResolveLegacyAssembly;
            Assembly loginEngineAssembly = Assembly.LoadFrom(assemblyPath);
            if (!string.Equals(loginEngineAssembly.GetName().Name, "LoginEngine", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the legacy LoginEngine assembly, found " + loginEngineAssembly.FullName + ".");
            }

            Assembly coreAssembly = typeof(AORebirth.Core.Components.IBus).Assembly;
            if (args[0] == "write")
            {
                Stage7ContractFingerprint.WriteLegacy(args[1], loginEngineAssembly, coreAssembly);
            }
            else
            {
                Stage7ContractFingerprint.VerifyLegacy(args[1], loginEngineAssembly, coreAssembly);
            }

            Console.WriteLine("PASS: Stage 7 legacy LoginEngine contracts");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + Unwrap(exception).Message);
            return 1;
        }
    }

    private static Assembly ResolveLegacyAssembly(object sender, ResolveEventArgs args)
    {
        string simpleName = new AssemblyName(args.Name).Name;
        string dllPath = Path.Combine(assemblyDirectory, simpleName + ".dll");
        if (File.Exists(dllPath)) return Assembly.LoadFrom(dllPath);
        string exePath = Path.Combine(assemblyDirectory, simpleName + ".exe");
        return File.Exists(exePath) ? Assembly.LoadFrom(exePath) : null;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException && exception.InnerException != null)
        {
            exception = exception.InnerException;
        }

        return exception;
    }
}
