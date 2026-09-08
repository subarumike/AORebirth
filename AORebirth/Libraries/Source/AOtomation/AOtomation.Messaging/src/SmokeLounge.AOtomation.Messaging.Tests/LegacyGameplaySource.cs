namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Physical partial extraction does not change a source contract's logical ownership.
    /// Read every compiled fragment; do not weaken existing packet/evidence assertions.
    /// </summary>
    internal static class LegacyGameplaySource
    {
        static readonly Dictionary<string, string[]> Fragments = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "CapturedEnemyCombatContract.cs", new[] { "CapturedEnemyCombatData.cs", "CapturedEnemyCombatSequenceData.cs", "CapturedEnemyCombatContract.Data.cs" } },
            { "CapturedEnemyCombatProfileCatalog.cs", new[] { "CapturedEnemyCombatProfileData.cs", "CapturedEnemyCombatProfileMatching.cs" } },
            { "CapturedEnemyCombatPacketFactory.cs", new[] { "CapturedEnemyCombatPacketFactory.Data.cs" } },
            { "OrdinaryEnemyCombatSetupGenerator.cs", new[] { "OrdinaryEnemyCombatSetupGenerator.Data.cs" } },
            { "SafeQuestFullUpdateSender.cs", new[] { "SafeQuestFullUpdateSender.AuthoredData.cs" } }
        };

        internal static string ReadAllText(string path)
        {
            string source = File.ReadAllText(path);
            string[] fragments;
            if (Fragments.TryGetValue(Path.GetFileName(path), out fragments))
            {
                foreach (string fragment in fragments)
                    source += Environment.NewLine + File.ReadAllText(Path.Combine(Path.GetDirectoryName(path), fragment));
            }
            return source;
        }

        internal static string[] LogicalPaths(IEnumerable<string> paths)
        {
            return paths.Select(path =>
            {
                string file = Path.GetFileName(path);
                foreach (KeyValuePair<string, string[]> owner in Fragments)
                    if (owner.Value.Contains(file)) return Path.Combine(Path.GetDirectoryName(path), owner.Key);
                return path;
            }).Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
