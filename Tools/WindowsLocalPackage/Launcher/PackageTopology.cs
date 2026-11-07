namespace AORebirth.WindowsLocalLauncher
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Web.Script.Serialization;

    internal static class PackageTopology
    {
        internal const string DailyPhp = "DailyLoginPhp";
        internal const string DailyBindAddress = "127.0.0.1";
        internal const int DailyPort = 80;
        internal const string DailyHost = "uwg.daily.icc-rk";
        private static readonly string[] Engines = { "WorldEngine", "ChatEngine", "LoginEngine", "ZoneEngine_New" };
        private static readonly string[] Shutdown = { DailyPhp, "AccountBroker", "ZoneEngine_New", "LoginEngine", "ChatEngine", "WorldEngine", "MySQL" };

        internal static int[] FixedPorts { get { return new[] { DailyPort, 7012, 7500, 7501, 7510, 7600, 7601 }; } }

        internal static bool IsEngine(string name) { return Engines.Contains(name, StringComparer.Ordinal); }

        internal static void StartEngines(Action<string, int[]> start)
        {
            foreach (string name in Engines)
                start(name, Ports(name, 0));
        }

        internal static string[] StopOrder(IEnumerable<string> names)
        {
            string[] components = names.ToArray();
            if (components.Distinct(StringComparer.Ordinal).Count() != components.Length
                || components.Any(name => !Shutdown.Contains(name, StringComparer.Ordinal)))
                throw new InvalidDataException("The package contains an unknown or duplicate running component.");

            return components.OrderBy(name => Array.IndexOf(Shutdown, name)).ToArray();
        }

        internal static int[] Ports(string name, int databasePort)
        {
            switch (name)
            {
                case "MySQL": return new[] { databasePort };
                case "WorldEngine": return new[] { 7600, 7601 };
                case "ChatEngine": return new[] { 7012 };
                case "LoginEngine": return new[] { 7500 };
                case "ZoneEngine_New": return new[] { 7501 };
                case "AccountBroker": return new[] { 7510 };
                case DailyPhp: return new[] { DailyPort };
                default: throw new InvalidDataException("Unknown package component: " + name);
            }
        }

        internal static bool IsWorldReady(string json, bool requireRegistrations)
        {
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
                var document = serializer.Deserialize<Dictionary<string, object>>(json);
                object raw;
                if (document == null || !document.TryGetValue("world", out raw))
                    return false;

                var world = raw as Dictionary<string, object>;
                if (world == null || !world.TryGetValue("epoch", out raw) || !(raw is int || raw is long)
                    || Convert.ToInt64(raw, CultureInfo.InvariantCulture) <= 0
                    || !world.TryGetValue("recovering", out raw) || !(raw is bool))
                    return false;

                bool recovering = (bool)raw;
                if (!world.TryGetValue("instances", out raw) || !(raw is IEnumerable) || raw is string)
                    return false;

                var instances = new List<Dictionary<string, object>>();
                foreach (object instance in (IEnumerable)raw)
                {
                    var entry = instance as Dictionary<string, object>;
                    if (entry == null)
                        return false;

                    instances.Add(entry);
                }

                if (!requireRegistrations)
                    return true;

                // Login uses World RPCs; only Chat and Zone own registry streams.
                return !recovering
                    && HasRegistration(instances, "chat", "RoleChat", null)
                    && HasRegistration(instances, "zone-7501", "RoleZone", "127.0.0.1:7501");
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException
                || exception is FormatException || exception is OverflowException)
            {
                return false;
            }
        }

        private static bool HasRegistration(IEnumerable<Dictionary<string, object>> instances, string name, string role, string endpoint)
        {
            Dictionary<string, object>[] matches = instances.Where(instance =>
                instance.ContainsKey("name") && string.Equals(instance["name"] as string, name, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
                return false;

            Dictionary<string, object> entry = matches[0];
            object raw;
            return entry.TryGetValue("role", out raw) && string.Equals(raw as string, role, StringComparison.Ordinal)
                && entry.TryGetValue("connected", out raw) && raw is bool && (bool)raw
                && entry.TryGetValue("heartbeatAgeMs", out raw) && (raw is int || raw is long)
                && Convert.ToInt64(raw, CultureInfo.InvariantCulture) >= 0
                && Convert.ToInt64(raw, CultureInfo.InvariantCulture) <= 15000
                && (endpoint == null || entry.TryGetValue("endpoint", out raw)
                    && string.Equals(raw as string, endpoint, StringComparison.Ordinal));
        }
    }
}
