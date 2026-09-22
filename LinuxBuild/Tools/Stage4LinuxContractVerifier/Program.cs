using System;
using AORebirth.Communication.Messages;
using AORebirth.LinuxBuild;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: Stage4LinuxContractVerifier <legacy-manifest-path>");
                return 2;
            }

            Stage4ContractFingerprint.VerifyLinux(args[0], typeof(MessageBase).Assembly);
            Console.WriteLine("PASS: Stage 4 Linux Communication contracts");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }
}
