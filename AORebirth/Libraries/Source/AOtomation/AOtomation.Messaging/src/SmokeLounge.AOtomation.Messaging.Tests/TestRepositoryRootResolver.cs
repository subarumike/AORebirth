namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal static class TestRepositoryRootResolver
    {
        internal static string FindFromCallerFilePath(
            string explicitRepositoryRoot = null,
            [CallerFilePath] string sourcePath = null)
        {
            return Resolve(sourcePath, explicitRepositoryRoot);
        }

        internal static string Resolve(string startingPath, string explicitRepositoryRoot = null)
        {
            if (!string.IsNullOrWhiteSpace(explicitRepositoryRoot))
            {
                string explicitCandidate = Normalize(explicitRepositoryRoot);
                if (!IsRepositoryRoot(explicitCandidate))
                {
                    throw new InvalidOperationException(
                        "Explicit AORebirth repository root is invalid. Attempted root: "
                        + explicitCandidate
                        + ". Expected .git (file or directory), AI_START_HERE.md, AGENTS.md, "
                        + "AORebirth/Server/ZoneEngine, and docs/ai/WORKFLOW.md.");
                }

                return explicitCandidate;
            }

            if (string.IsNullOrWhiteSpace(startingPath))
            {
                throw new InvalidOperationException(
                    "AORebirth repository root discovery requires a starting file or directory path.");
            }

            string normalizedStart = Normalize(startingPath);
            string startingDirectory = File.Exists(normalizedStart)
                ? Path.GetDirectoryName(normalizedStart)
                : normalizedStart;
            var current = new DirectoryInfo(startingDirectory);
            while (current != null)
            {
                if (IsRepositoryRoot(current.FullName))
                {
                    return Normalize(current.FullName);
                }

                current = current.Parent;
            }

            throw new InvalidOperationException(
                "AORebirth repository root was not found. Starting path: "
                + normalizedStart
                + ". Expected .git (file or directory), AI_START_HERE.md, AGENTS.md, "
                + "AORebirth/Server/ZoneEngine, and docs/ai/WORKFLOW.md in one parent directory.");
        }

        internal static bool IsRepositoryRoot(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            string normalizedCandidate = Normalize(candidate);
            string gitMarker = Path.Combine(normalizedCandidate, ".git");
            return (Directory.Exists(gitMarker) || File.Exists(gitMarker))
                && File.Exists(Path.Combine(normalizedCandidate, "AI_START_HERE.md"))
                && File.Exists(Path.Combine(normalizedCandidate, "AGENTS.md"))
                && Directory.Exists(
                    Path.Combine(normalizedCandidate, "AORebirth", "Server", "ZoneEngine"))
                && File.Exists(
                    Path.Combine(normalizedCandidate, "docs", "ai", "WORKFLOW.md"));
        }

        private static string Normalize(string path)
        {
            string normalized = Path.GetFullPath(path);
            string root = Path.GetPathRoot(normalized);
            if (!string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            }

            return normalized;
        }
    }
}
