using System;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: Stage5LinuxContractVerifier <legacy-manifest-path> <repository-root>");
                return 2;
            }

            Stage5ContractFingerprint.VerifyLinux(
                args[0],
                typeof(ChatEngine.PacketWriter).Assembly,
                typeof(AO.Core.Encryption.BigInteger).Assembly);
            Stage5RepositoryChecks.VerifyRepository(args[1]);
            Console.WriteLine("PASS: Stage 5 Linux ChatEngine contracts");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }
}
