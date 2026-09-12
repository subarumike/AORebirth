namespace ZoneEngine_New
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using AORebirth.Core.Playfields.OfficialPlacements;
    using MySqlConnector;
    using Utility.Config;
    using Utility.Network;
    using ZoneEngine_New.Core.Data;

    /// <summary>Read-only startup contracts shared by both OS entry points.</summary>
    public static class RuntimeStartup
    {
        public static void ValidateArguments(string[] args)
        {
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "/headless", "--headless", "/autostart", "--autostart",
                "--validate-startup", "--validate-database", "--validate-official-placements"
            };
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "/shutdown-file", "--shutdown-file", "/stdout-log", "--stdout-log",
                "/stderr-log", "--stderr-log", "--source-sha", "--build-platform",
                "--placement-manifest-output", "--placement-provenance-output"
            };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int validationModes = 0;
            for (int i = 0; i < args.Length; i++)
            {
                string option = args[i];
                if (!seen.Add(option.TrimStart('-', '/')))
                    throw new ArgumentException("Duplicate option.");
                if (flags.Contains(option))
                {
                    if (option.StartsWith("--validate-", StringComparison.OrdinalIgnoreCase)) validationModes++;
                    continue;
                }
                if (!values.Contains(option) || ++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
                    throw new ArgumentException("Unknown option or missing value.");
                if (flags.Contains(args[i]) || values.Contains(args[i]))
                    throw new ArgumentException("Option value cannot be another option.");
            }
            if (validationModes > 1) throw new ArgumentException("Validation modes are mutually exclusive.");
        }

        public static void ValidateConfiguration()
        {
            Config config = ConfigReadWrite.Instance.CurrentConfig
                ?? throw new InvalidOperationException("Configuration is required.");
            EngineBindPolicy.ResolveFromEnvironment();
            ValidatePort(config.ZonePort);
            ValidatePort(config.CommPort);
            if (!IPAddress.TryParse(config.ZoneIP, out IPAddress? advertised)
                || advertised.AddressFamily != AddressFamily.InterNetwork
                || advertised.Equals(IPAddress.Any) || advertised.Equals(IPAddress.Broadcast))
                throw new InvalidOperationException("ZoneIP must be a concrete advertised IPv4 address.");
            if (!IPAddress.TryParse(config.ChatIP, out IPAddress? chat)
                || chat.Equals(IPAddress.Any) || chat.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("ChatIP must be a concrete address.");
            if (!string.Equals(config.SQLType, "MySql", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ZoneEngine_New requires MySql.");
            var connection = new MySqlConnectionStringBuilder(MySqlConnectionSettings.GetRequiredConnectionString());
            if (string.IsNullOrWhiteSpace(connection.Server) || string.IsNullOrWhiteSpace(connection.Database)
                || string.IsNullOrWhiteSpace(connection.UserID))
                throw new InvalidOperationException("MySQL target is incomplete.");
            ValidateDatabaseTarget(connection.Database, Environment.GetEnvironmentVariable("AO_REBIRTH_EXPECTED_DATABASE"),
                string.Equals(Environment.GetEnvironmentVariable("AO_REBIRTH_REQUIRED_SQL_TYPE"), "MySql", StringComparison.Ordinal));
        }

        public static void ValidateDatabaseTarget(string database, string? expected, bool deploymentProfile)
        {
            if (deploymentProfile && string.IsNullOrWhiteSpace(expected))
                throw new StartupValidationException("AO_REBIRTH_EXPECTED_DATABASE is required by the deployment profile.");
            if (expected != null && !string.Equals(database, expected, StringComparison.Ordinal))
                throw new StartupValidationException("Selected database does not match AO_REBIRTH_EXPECTED_DATABASE.");
        }

        public static void ValidatePackage(string baseDirectory)
        {
            string gameData = Path.Combine(baseDirectory, "GameData");
            foreach (string file in new[] { "MobTemplates.json", "ItemTemplates.json", "HashInstances.json",
                "VendingMachines.json", "MonsterData.json", "Xp.json" })
            {
                using FileStream stream = File.OpenRead(Path.Combine(gameData, file));
                using JsonDocument document = JsonDocument.Parse(stream);
            }
            AORebirth.World.Package.PlayfieldPackageValidator.Validate(Path.Combine(gameData, "Playfields"),
                Path.Combine(baseDirectory, "Content", "Official", "PlayfieldPlacements", "playfield-package-manifest.json"));
            using (FileStream items = File.OpenRead(Path.Combine(gameData, "items.dat")))
                if (items.Length == 0) throw new InvalidDataException("Packaged item catalog is empty.");
            _ = new OfficialPlayfieldPlacementCatalog(
                OfficialPlayfieldPlacementCatalog.ResolveRuntimeCorpusRoot(baseDirectory));
        }

        public static void ValidatePort(int port)
        {
            if (port < 1 || port > 65535) throw new InvalidOperationException("Configured port is invalid.");
        }

        internal static void NotifyService(string message)
        {
            string? endpoint = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
            if (OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(endpoint)) return;
            if (endpoint[0] == '@') endpoint = "\0" + endpoint.Substring(1);
            using var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
            socket.SendTo(Encoding.UTF8.GetBytes(message), new UnixDomainSocketEndPoint(endpoint));
        }
    }

    public sealed class StartupValidationException : Exception
    {
        public StartupValidationException(string message) : base(message) { }
    }
}
