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
                Console.Error.WriteLine("Usage: LegacyStage5ContractTool <write|verify> <manifest-path> <legacy-ChatEngine.exe>");
                return 2;
            }

            string assemblyPath = Path.GetFullPath(args[2]);
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("Legacy ChatEngine assembly was not found.", assemblyPath);
            }

            assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            AppDomain.CurrentDomain.AssemblyResolve += ResolveLegacyAssembly;
            Assembly chatEngineAssembly = Assembly.LoadFrom(assemblyPath);
            if (!string.Equals(chatEngineAssembly.GetName().Name, "ChatEngine", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected the legacy ChatEngine assembly, found " + chatEngineAssembly.FullName + ".");
            }

            if (args[0] == "write")
            {
                Stage5ContractFingerprint.WriteLegacy(args[1], chatEngineAssembly);
            }
            else
            {
                Stage5ContractFingerprint.VerifyLegacy(args[1], chatEngineAssembly);
            }

            Console.WriteLine("PASS: Stage 5 legacy ChatEngine contracts");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }

    private static Assembly ResolveLegacyAssembly(object sender, ResolveEventArgs args)
    {
        string simpleName = new AssemblyName(args.Name).Name;
        string dllPath = Path.Combine(assemblyDirectory, simpleName + ".dll");
        if (File.Exists(dllPath))
        {
            return Assembly.LoadFrom(dllPath);
        }

        string exePath = Path.Combine(assemblyDirectory, simpleName + ".exe");
        return File.Exists(exePath) ? Assembly.LoadFrom(exePath) : null;
    }
}
