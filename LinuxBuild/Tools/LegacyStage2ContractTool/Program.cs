using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AORebirth.LinuxBuild.LegacyStage2ContractTool
{
    internal static class Program
    {
        private static readonly string[] AssemblyNames =
        {
            "AORebirth.Enums",
            "AORebirth.Core.Exceptions",
            "AORebirth.Interfaces",
            "AORebirth.ObjectManager"
        };

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 2 || (args[0] != "write" && args[0] != "verify"))
                {
                    throw new ArgumentException("Usage: LegacyStage2ContractTool (write|verify) <manifest-path>");
                }

                string manifestPath = Path.GetFullPath(args[1]);
                Assembly[] assemblies = AssemblyNames.Select(LoadLegacyAssembly).ToArray();
                if (args[0] == "write")
                {
                    Stage2ContractFingerprint.Write(manifestPath, assemblies);
                    Console.WriteLine("WROTE: " + manifestPath);
                }
                else
                {
                    Stage2ContractFingerprint.Verify(manifestPath, assemblies);
                    Console.WriteLine("PASS: legacy Stage 2 public-contract verification");
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
                    "The approved Windows Debug build must exist before generating the Stage 2 contract: " + assemblyName,
                    path);
            }

            return Assembly.LoadFrom(path);
        }
    }
}
