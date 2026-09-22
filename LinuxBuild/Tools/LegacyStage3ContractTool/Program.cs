using System;
using System.IO;
using System.Reflection;

namespace AORebirth.LinuxBuild.LegacyStage3ContractTool
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 2 || (args[0] != "write" && args[0] != "verify"))
                {
                    throw new ArgumentException("Usage: LegacyStage3ContractTool (write|verify) <manifest-path>");
                }

                Assembly databaseAssembly = LoadLegacyAssembly("AORebirth.Database");
                Assembly statsAssembly = LoadLegacyAssembly("AORebirth.Stats");
                string manifestPath = Path.GetFullPath(args[1]);
                if (args[0] == "write")
                {
                    Stage3ContractFingerprint.WriteLegacy(manifestPath, databaseAssembly, statsAssembly);
                    Console.WriteLine("WROTE: " + manifestPath);
                }
                else
                {
                    Stage3ContractFingerprint.VerifyLegacy(manifestPath, databaseAssembly, statsAssembly);
                    Console.WriteLine("PASS: legacy Stage 3 contract verification (no database connection)");
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception.Message);
                return 1;
            }
        }

        private static Assembly LoadLegacyAssembly(string assemblyName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName + ".dll");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The approved Windows Debug build must exist before generating the Stage 3 contract: " + assemblyName,
                    path);
            }

            return Assembly.LoadFrom(path);
        }
    }
}
