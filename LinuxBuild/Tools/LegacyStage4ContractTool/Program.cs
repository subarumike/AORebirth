using System;
using AORebirth.Communication.Messages;
using AORebirth.LinuxBuild;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2
                || (!string.Equals(args[0], "write", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(args[0], "verify", StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("Usage: LegacyStage4ContractTool <write|verify> <manifest-path>");
                return 2;
            }

            if (string.Equals(args[0], "write", StringComparison.OrdinalIgnoreCase))
            {
                Stage4ContractFingerprint.WriteLegacy(args[1], typeof(MessageBase).Assembly);
            }
            else
            {
                Stage4ContractFingerprint.VerifyLegacy(args[1], typeof(MessageBase).Assembly);
            }

            Console.WriteLine("PASS: Stage 4 legacy Communication contracts");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }
}
