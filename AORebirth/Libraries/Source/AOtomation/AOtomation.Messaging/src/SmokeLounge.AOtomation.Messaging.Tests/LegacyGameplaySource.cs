namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Reads retained historical fixtures with their shared compiled fragments.
    /// Removed Legacy orchestration is deliberately not reconstructed.
    /// </summary>
    internal static class LegacyGameplaySource
    {
        static readonly Dictionary<string, string[]> Fragments = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
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
                    source += Environment.NewLine + File.ReadAllText(ResolveFragment(path, fragment));
            }
            return source;
        }

        internal static string[] LogicalPaths(IEnumerable<string> paths)
        {
            return paths.Select(path =>
            {
                string file = Path.GetFileName(path);
                foreach (KeyValuePair<string, string[]> owner in Fragments)
                    if (owner.Value.Contains(file)) return ResolveFragment(path, owner.Key);
                return path;
            }).Distinct(StringComparer.Ordinal).ToArray();
        }

        internal static string ResolveFragment(string ownerPath, string fileName)
        {
            string sibling = Path.Combine(Path.GetDirectoryName(ownerPath), fileName);
            if (File.Exists(sibling)) return sibling;
            string root = TestRepositoryRootResolver.FindFromCallerFilePath();
            foreach (string relative in new[]
            {
                "AORebirth/Server/ZoneEngine_New/SharedGameplay/Combat",
                "AORebirth/Server/ZoneEngine_New/SharedGameplay/Missions",
                "Tests/Fixtures/Gameplay/Playfields",
                "Tests/Fixtures/Gameplay/Packets"
            })
            {
                string candidate = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException("Retained gameplay fixture fragment is missing", fileName);
        }
    }
}
