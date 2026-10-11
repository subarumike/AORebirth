namespace AORebirth.WindowsLocalLauncher
{
    using System;
    using System.IO;
    using System.Security.AccessControl;
    using System.Security.Cryptography;
    using System.Security.Principal;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    internal sealed class PackageState
    {
        internal string ClientFolder = string.Empty;
        internal string AdminPassword;
        internal string RuntimePassword;
        internal bool OwnsDataDirectory;
        internal bool SetupComplete;
        internal int DatabasePort = 3308;

        internal static PackageState Load(string root)
        {
            var state = new PackageState();
            string settings = Path.Combine(root, "data", "launcher.xml");
            if (File.Exists(settings))
            {
                XElement value = ReadXml(settings);
                state.ClientFolder = (string)value.Attribute("clientFolder") ?? string.Empty;
                state.OwnsDataDirectory = (bool?)value.Attribute("ownsDataDirectory") ?? false;
                state.SetupComplete = (bool?)value.Attribute("setupComplete") ?? false;
                state.DatabasePort = (int?)value.Attribute("databasePort") ?? 3308;
                if (state.DatabasePort < 1024 || state.DatabasePort > 65535)
                    throw new InvalidDataException("The saved local database port is invalid.");
            }

            string secrets = Path.Combine(root, "data", "launcher.secrets");
            if (File.Exists(secrets))
            {
                byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(secrets), null, DataProtectionScope.CurrentUser);
                try
                {
                    string[] values = Encoding.UTF8.GetString(clear).Split('\n');
                    if (values.Length != 2 || values[0].Length < 32 || values[1].Length < 32)
                        throw new InvalidDataException("The saved local credentials are invalid.");

                    state.AdminPassword = values[0];
                    state.RuntimePassword = values[1];
                }
                finally
                {
                    Array.Clear(clear, 0, clear.Length);
                }
            }
            else if (state.OwnsDataDirectory || state.SetupComplete)
            {
                throw new InvalidDataException("Local credentials are missing. Restore data/launcher.secrets with your database backup.");
            }

            return state;
        }

        internal void Save(string root)
        {
            string data = Path.Combine(root, "data");
            Directory.CreateDirectory(data);
            RestrictDirectory(data);
            if (AdminPassword == null)
            {
                AdminPassword = NewPassword();
                RuntimePassword = NewPassword();
            }

            string secretPath = Path.Combine(data, "launcher.secrets");
            byte[] clear = Encoding.UTF8.GetBytes(AdminPassword + "\n" + RuntimePassword);
            try
            {
                AtomicWrite(secretPath, ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser));
            }
            finally
            {
                Array.Clear(clear, 0, clear.Length);
            }

            var document = new XDocument(new XElement("Launcher",
                new XAttribute("clientFolder", ClientFolder),
                new XAttribute("ownsDataDirectory", OwnsDataDirectory),
                new XAttribute("setupComplete", SetupComplete),
                new XAttribute("databasePort", DatabasePort)));
            AtomicWrite(Path.Combine(data, "launcher.xml"), Encoding.UTF8.GetBytes(document.ToString()));
        }

        internal static XElement ReadXml(string path)
        {
            using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                return XDocument.Load(reader).Root;
            }
        }

        private static void RestrictDirectory(string path)
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            foreach (SecurityIdentifier sid in new[]
            {
                WindowsIdentity.GetCurrent().User,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
            })
            {
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inheritance,
                    PropagationFlags.None, AccessControlType.Allow));
            }

            Directory.SetAccessControl(path, security);
        }

        private static string NewPassword()
        {
            byte[] bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes);
        }

        internal static void AtomicWrite(string path, byte[] bytes)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".new";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
                catch (IOException)
                {
                    // An unremoved unique temporary file cannot block the next save.
                }
                catch (UnauthorizedAccessException)
                {
                    // Preserve the original save result if cleanup access changes.
                }
            }
        }
    }
}
