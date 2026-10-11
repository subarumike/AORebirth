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
    }
}
