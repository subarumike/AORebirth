using System;
using System.IO;

namespace AORebirth.LinuxBuild.Stage8OfflineSmokeTests
{
    internal static class Stage8RepositoryRootResolverTests
    {
        public static void Run()
        {
            string temporaryParent = Path.Combine(
                Path.GetTempPath(),
                "AORebirth Stage8 Root Tests " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryParent);
            try
            {
                ConventionalCheckoutDirectoryResolves(temporaryParent);
                LinkedWorktreeFileResolves(temporaryParent);
                NestedExecutionDirectoryResolves(temporaryParent);
                PathContainingSpacesResolves(temporaryParent);
                InvalidExplicitRootFailsWithActionablePaths(temporaryParent);
            }
            finally
            {
                string normalizedTemporaryParent = Path.GetFullPath(temporaryParent);
                if (Directory.Exists(normalizedTemporaryParent)
                    && normalizedTemporaryParent.StartsWith(
                        Path.GetFullPath(Path.GetTempPath()),
                        StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Delete(normalizedTemporaryParent, true);
                }
            }

            Console.WriteLine("PASS: Stage 8 repository-root path tests 5/5");
        }

        private static void ConventionalCheckoutDirectoryResolves(string temporaryParent)
        {
            string root = CreateRepository(temporaryParent, "conventional", false);
            RequireEqual(root, Stage8RepositoryRootResolver.ResolveExplicit(root));
        }

        private static void LinkedWorktreeFileResolves(string temporaryParent)
        {
            string root = CreateRepository(temporaryParent, "linked", true);
            RequireEqual(root, Stage8RepositoryRootResolver.ResolveExplicit(root));
        }

        private static void NestedExecutionDirectoryResolves(string temporaryParent)
        {
            string root = CreateRepository(temporaryParent, "nested", true);
            string nested = Path.Combine(root, "LinuxBuild", "artifacts", "Stage8", "bin");
            Directory.CreateDirectory(nested);
            RequireEqual(root, Stage8RepositoryRootResolver.FindFrom(nested));
        }

        private static void PathContainingSpacesResolves(string temporaryParent)
        {
            string root = CreateRepository(temporaryParent, "linked checkout with spaces", true);
            RequireEqual(root, Stage8RepositoryRootResolver.ResolveExplicit(root));
        }

        private static void InvalidExplicitRootFailsWithActionablePaths(string temporaryParent)
        {
            string root = CreateRepository(temporaryParent, "valid-parent", true);
            string invalidRoot = Path.Combine(root, "nested-invalid-root");
            Directory.CreateDirectory(invalidRoot);

            try
            {
                Stage8RepositoryRootResolver.ResolveExplicit(invalidRoot);
                throw new InvalidOperationException("Invalid Stage 8 root was accepted.");
            }
            catch (InvalidOperationException exception)
            {
                Require(
                    exception.Message.Contains(Path.GetFullPath(invalidRoot)),
                    "Invalid-root diagnostic omitted the attempted root.");
                Require(
                    exception.Message.Contains(Path.Combine(invalidRoot, ".git")),
                    "Invalid-root diagnostic omitted the expected path.");
            }
        }

        private static string CreateRepository(
            string temporaryParent,
            string directoryName,
            bool linkedWorktree)
        {
            string root = Path.GetFullPath(Path.Combine(temporaryParent, directoryName));
            Directory.CreateDirectory(root);
            string gitMarker = Path.Combine(root, ".git");
            if (linkedWorktree)
            {
                File.WriteAllText(gitMarker, "gitdir: test-only-linked-worktree-metadata");
            }
            else
            {
                Directory.CreateDirectory(gitMarker);
            }

            WriteSentinel(root, "AI_START_HERE.md");
            WriteSentinel(root, Path.Combine("AORebirth", "Server", "ZoneEngine", "Program.cs"));
            WriteSentinel(
                root,
                Path.Combine(
                    "LinuxBuild",
                    "Tools",
                    "Stage8OfflineSmokeTests",
                    "Stage8OfflineSmokeTests.csproj"));
            return root;
        }

        private static void WriteSentinel(string root, string relativePath)
        {
            string path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "test-only sentinel");
        }

        private static void RequireEqual(string expected, string actual)
        {
            Require(
                string.Equals(
                    Path.GetFullPath(expected),
                    Path.GetFullPath(actual),
                    StringComparison.OrdinalIgnoreCase),
                "Resolved Stage 8 repository root was not deterministic. Expected: "
                + expected
                + ". Actual: "
                + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
