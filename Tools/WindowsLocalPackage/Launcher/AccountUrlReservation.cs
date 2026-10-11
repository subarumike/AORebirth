namespace AORebirth.WindowsLocalLauncher
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.AccessControl;
    using System.Security.Principal;

    internal static class AccountUrlReservation
    {
        private const string Prefix = "http://127.0.0.1:7510/";

        internal static void EnsureForCurrentUser()
        {
            SecurityIdentifier user = WindowsIdentity.GetCurrent().User;
            string existing = Query();
            if (existing != null)
            {
                if (!AllowsUser(existing, user))
                    throw new InvalidOperationException("The local account address is reserved for another user or program. Its reservation was preserved.");

                return;
            }

            try
            {
                using (Process helper = Process.Start(new ProcessStartInfo(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AORebirth.exe"),
                    "--reserve-account-url " + user.Value)
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    helper.WaitForExit();
                    if (helper.ExitCode != 0)
                        throw new InvalidOperationException("Windows account-address setup failed (exit " + helper.ExitCode + "). No existing reservation was replaced.");
                }
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
            {
                throw new InvalidOperationException("Windows permission was cancelled. Run setup again and allow the one-time local account-address registration.");
            }

            existing = Query();
            if (existing == null || !AllowsUser(existing, user))
                throw new InvalidOperationException("Windows did not confirm the local account-address permission.");
        }

        internal static int Reserve(string sidText)
        {
            try
            {
                var sid = new SecurityIdentifier(sidText);
                if (!sid.IsAccountSid() || sid.AccountDomainSid == null)
                    return 2;

                string existing = Query();
                if (existing != null)
                    return AllowsUser(existing, sid) ? 0 : 10;

                string netsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe");
                using (Process process = Process.Start(new ProcessStartInfo(netsh,
                    "http add urlacl url=" + Prefix + " sddl=D:(A;;GX;;;" + sid.Value + ")")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }))
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            catch
            {
                return 11;
            }
        }

        private static bool AllowsUser(string sddl, SecurityIdentifier user)
        {
            var descriptor = new RawSecurityDescriptor(sddl);
            if (descriptor.DiscretionaryAcl == null)
                return false;

            foreach (GenericAce ace in descriptor.DiscretionaryAcl)
            {
                var common = ace as CommonAce;
                if (common != null && common.AceQualifier == AceQualifier.AccessAllowed
                    && common.SecurityIdentifier == user && (common.AccessMask & 0x20000000) != 0)
                    return true;
            }

            return false;
        }

        private static string Query()
        {
            var version = new HttpVersion { Major = 2, Minor = 0 };
            uint initialized = HttpInitialize(version, 2, IntPtr.Zero);
            if (initialized != 0)
                throw new InvalidOperationException("Windows HTTP configuration could not be read (" + initialized + ").");

            IntPtr prefix = Marshal.StringToHGlobalUni(Prefix);
            IntPtr buffer = IntPtr.Zero;
            try
            {
                var query = new UrlQuery { QueryType = 0, Prefix = prefix, Token = 0 };
                int length;
                uint result = HttpQueryServiceConfiguration(IntPtr.Zero, 2, ref query, Marshal.SizeOf(typeof(UrlQuery)),
                    IntPtr.Zero, 0, out length, IntPtr.Zero);
                if (result == 2)
                    return null;

                if (result != 122)
                    throw new InvalidOperationException("Windows account-address configuration could not be queried (" + result + ").");

                buffer = Marshal.AllocHGlobal(length);
                result = HttpQueryServiceConfiguration(IntPtr.Zero, 2, ref query, Marshal.SizeOf(typeof(UrlQuery)),
                    buffer, length, out length, IntPtr.Zero);
                if (result != 0)
                    throw new InvalidOperationException("Windows account-address configuration could not be read (" + result + ").");

                var reservation = (UrlReservation)Marshal.PtrToStructure(buffer, typeof(UrlReservation));
                return Marshal.PtrToStringUni(reservation.SecurityDescriptor);
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }

                Marshal.FreeHGlobal(prefix);
                HttpTerminate(2, IntPtr.Zero);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HttpVersion { internal ushort Major; internal ushort Minor; }

        [StructLayout(LayoutKind.Sequential)]
        private struct UrlQuery { internal int QueryType; internal IntPtr Prefix; internal uint Token; }

        [StructLayout(LayoutKind.Sequential)]
        private struct UrlReservation { internal IntPtr Prefix; internal IntPtr SecurityDescriptor; }

        [DllImport("httpapi.dll")]
        private static extern uint HttpInitialize(HttpVersion version, uint flags, IntPtr reserved);

        [DllImport("httpapi.dll")]
        private static extern uint HttpTerminate(uint flags, IntPtr reserved);

        [DllImport("httpapi.dll")]
        private static extern uint HttpQueryServiceConfiguration(IntPtr service, int id, ref UrlQuery input,
            int inputSize, IntPtr output, int outputSize, out int returnSize, IntPtr overlapped);
    }
}
