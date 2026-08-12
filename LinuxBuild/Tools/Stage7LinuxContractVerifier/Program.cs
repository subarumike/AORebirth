using System;
using System.Reflection;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: Stage7LinuxContractVerifier <legacy-manifest-path> <repository-root>");
                return 2;
            }

            Stage7ContractFingerprint.VerifyLinux(
                args[0],
                typeof(LoginEngine.CoreClient.Client).Assembly,
                typeof(AORebirth.Core.Components.IBus).Assembly);
            Stage7ContractFingerprint.VerifyRepository(args[1]);
            Console.WriteLine("PASS: Stage 7 Linux LoginEngine contracts");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + Unwrap(exception).Message);
            return 1;
        }
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
