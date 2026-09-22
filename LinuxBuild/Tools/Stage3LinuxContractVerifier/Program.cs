using System;
using System.IO;
using System.Reflection;

namespace AORebirth.LinuxBuild.Stage3LinuxContractVerifier
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 1)
                {
                    throw new ArgumentException("Usage: Stage3LinuxContractVerifier <legacy-manifest-path>");
                }

                string manifestPath = Path.GetFullPath(args[0]);
                Assembly databaseAssembly = typeof(AORebirth.Database.Connector).Assembly;
                Assembly statsAssembly = typeof(AORebirth.Stats.Stats).Assembly;
                Stage3ContractFingerprint.VerifyLinux(manifestPath, databaseAssembly, statsAssembly);
                Console.WriteLine("PASS: Linux Stage 3 semantic, runtime, and mapped-reference contracts (no database connection)");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception.Message);
                return 1;
            }
        }
    }
}
