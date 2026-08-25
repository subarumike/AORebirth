using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AORebirth.LinuxBuild.Stage8OfflineSmokeTests
{
    internal static class Stage8RepositoryRootResolver
    {
        private static readonly string[] RepositorySentinels =
        {
            "AI_START_HERE.md",
            Path.Combine("AORebirth", "Server", "ZoneEngine", "Program.cs"),
            Path.Combine(
                "LinuxBuild",
                "Tools",
                "Stage8OfflineSmokeTests",
                "Stage8OfflineSmokeTests.csproj")
        };

        public static string ResolveExplicit(string repositoryRoot)
        {
            string normalizedRoot = NormalizeDirectory(repositoryRoot, "repository root");
            string failure = DescribeInvalidCandidate(normalizedRoot);
            if (failure == null)
            {
                return normalizedRoot;
            }

            throw new InvalidOperationException(
                "Invalid Stage 8 repository root. Attempted root: "
                + normalizedRoot
                + ". "
                + failure);
        }

        public static string FindFrom(string startingPath)
        {
            string normalizedStart = NormalizeDirectory(startingPath, "starting path");
            var attemptedRoots = new List<string>();
            DirectoryInfo current = new DirectoryInfo(normalizedStart);
            while (current != null)
            {
                attemptedRoots.Add(current.FullName);
                if (DescribeInvalidCandidate(current.FullName) == null)
                {
                    return Path.GetFullPath(current.FullName);
                }

                current = current.Parent;
            }

            throw new InvalidOperationException(
                "Stage 8 repository root was not found. Starting path: "
                + normalizedStart
                + ". Expected a .git file or directory and sentinels: "
                + string.Join(", ", RepositorySentinels)
                + ". Attempted roots: "
                + string.Join("; ", attemptedRoots));
        }

        public static string ResolveRequiredFile(
            string repositoryRoot,
            params string[] relativePathSegments)
        {
            string normalizedRoot = ResolveExplicit(repositoryRoot);
            string expectedPath = relativePathSegments.Aggregate(
                normalizedRoot,
                Path.Combine);
            if (!File.Exists(expectedPath))
            {
                throw new InvalidOperationException(
                    "Stage 8 source file was not found. Attempted root: "
                    + normalizedRoot
                    + ". Expected path: "
                    + expectedPath);
            }

            return expectedPath;
        }

        private static string NormalizeDirectory(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Stage 8 " + parameterName + " must not be empty.",
                    parameterName);
            }

            string normalizedPath = Path.GetFullPath(path);
            if (File.Exists(normalizedPath))
            {
                DirectoryInfo parent = new FileInfo(normalizedPath).Directory;
                if (parent == null)
                {
                    throw new InvalidOperationException(
                        "Stage 8 " + parameterName + " has no parent directory: " + normalizedPath);
                }

                return parent.FullName;
            }

            return new DirectoryInfo(normalizedPath).FullName;
        }

        private static string DescribeInvalidCandidate(string candidate)
        {
            string gitMarker = Path.Combine(candidate, ".git");
            if (!Directory.Exists(gitMarker) && !File.Exists(gitMarker))
            {
                return "Expected a .git file or directory at: " + gitMarker;
            }

            for (int index = 0; index < RepositorySentinels.Length; index++)
            {
                string expectedPath = Path.Combine(candidate, RepositorySentinels[index]);
                if (!File.Exists(expectedPath))
                {
                    return "Expected repository sentinel at: " + expectedPath;
                }
            }

            return null;
        }
    }
}
