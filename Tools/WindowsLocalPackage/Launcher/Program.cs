namespace AORebirth.WindowsLocalLauncher
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Windows.Forms;

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            if (args.Length == 2 && args[0] == "--reserve-account-url")
                return AccountUrlReservation.Reserve(args[1]);

            if (args.Length == 1 && args[0] == "--verify-package")
            {
                try
                {
                    PackageController.VerifyPackage(root);
                    Console.WriteLine("PACKAGE_INVENTORY_AND_CONFIGURATION_VERIFIED");
                    return 0;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("PACKAGE_VERIFICATION_FAILED: " + exception.Message);
                    return 1;
                }
            }

            if (args.Length == 1 && args[0] == "--verify-startup")
                return VerifyStartup(root);

            if (args.Length == 1 && args[0] == "--stop-owned")
                return StopOwned(root);

            if (args.Length != 0)
                return 2;

            bool created;
            using (var mutex = new Mutex(true, "Local\\AORebirthWindowsLocalLauncher", out created))
            {
                if (!created)
                {
                    MessageBox.Show("AORebirth is already open. Use the existing launcher window.", "AORebirth");
                    return 1;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LauncherForm(root));
                return 0;
            }
        }

        private static int VerifyStartup(string root)
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\AORebirthWindowsLocalLauncher", out created))
            {
                if (!created)
                {
                    Console.Error.WriteLine("FOUR_ENGINE_STARTUP_REFUSED: close the existing launcher first.");
                    return 1;
                }

                PackageController controller = null;
                int result = 0;
                try
                {
                    PackageController.RequireFreshStartupData(root);
                    controller = new PackageController(root, Console.WriteLine);
                    controller.Prepare();
                    Console.WriteLine("FOUR_ENGINE_STARTUP_TIMEOUT_SECONDS=" + PackageController.StartupTimeoutMilliseconds / 1000);
                    controller.Start();
                    controller.CheckStatus();
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("FOUR_ENGINE_STARTUP_FAILED: " + exception.Message);
                    result = 1;
                }
                finally
                {
                    if (controller != null)
                    {
                        try
                        {
                            controller.Stop();
                            if (!controller.LastStopWasClean)
                            {
                                Console.Error.WriteLine("FOUR_ENGINE_SHUTDOWN_FAILED: a service required forced shutdown or exited abnormally.");
                                result = 1;
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.Error.WriteLine("FOUR_ENGINE_SHUTDOWN_FAILED: " + exception.Message);
                            result = 1;
                        }
                    }
                }

                if (result == 0)
                    Console.WriteLine("FOUR_ENGINE_STARTUP_VERIFIED: World, Chat, Login and Zone ready; World registrations and local Daily Rewards GET/POST verified; owned services stopped.");
                return result;
            }
        }

        private static int StopOwned(string root)
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\AORebirthWindowsLocalLauncher", out created))
            {
                if (!created)
                {
                    Console.Error.WriteLine("OWNED_SHUTDOWN_REFUSED: the existing launcher still owns its lifecycle.");
                    return 1;
                }
                try
                {
                    var controller = new PackageController(root, Console.WriteLine);
                    controller.Stop();
                    if (!controller.LastStopWasClean)
                        return 1;
                    Console.WriteLine("OWNED_SHUTDOWN_VERIFIED");
                    return 0;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("OWNED_SHUTDOWN_FAILED: " + exception.Message);
                    return 1;
                }
            }
        }
    }
}
