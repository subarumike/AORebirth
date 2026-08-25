namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TestRepositoryRootResolverTests
    {
        private string temporaryRoot;

        [TestInitialize]
        public void Initialize()
        {
            this.temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "AORebirth repository root tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.temporaryRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            string normalized = Path.GetFullPath(this.temporaryRoot);
            string expectedParent = Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "AORebirth repository root tests"));
            if (normalized.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(normalized))
            {
                Directory.Delete(normalized, true);
            }
        }

        [TestMethod]
        public void ConventionalCheckoutGitDirectoryIsAccepted()
        {
            string repositoryRoot = this.CreateRepository("conventional checkout", false);

            Assert.AreEqual(
                Path.GetFullPath(repositoryRoot),
                TestRepositoryRootResolver.Resolve(repositoryRoot));
        }

        [TestMethod]
        public void LinkedWorktreeGitFileIsAccepted()
        {
            string repositoryRoot = this.CreateRepository("linked worktree", true);

            Assert.AreEqual(
                Path.GetFullPath(repositoryRoot),
                TestRepositoryRootResolver.Resolve(repositoryRoot));
        }

        [TestMethod]
        public void NestedTestOutputDirectoryResolvesItsRepository()
        {
            string repositoryRoot = this.CreateRepository("nested checkout", true);
            string nested = Path.Combine(repositoryRoot, "bin", "test", "Debug", "net48");
            Directory.CreateDirectory(nested);

            Assert.AreEqual(
                Path.GetFullPath(repositoryRoot),
                TestRepositoryRootResolver.Resolve(nested));
        }

        [TestMethod]
        public void PathContainingSpacesAndAlternateSeparatorsResolvesDeterministically()
        {
            string repositoryRoot = this.CreateRepository("checkout with spaces", true);
            string nested = Path.Combine(repositoryRoot, "folder with spaces", "nested");
            Directory.CreateDirectory(nested);
            string alternate = nested.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            Assert.AreEqual(
                Path.GetFullPath(repositoryRoot),
                TestRepositoryRootResolver.Resolve(alternate));
        }

        [TestMethod]
        public void InvalidExplicitOverrideFailsWithoutFilesystemFallback()
        {
            string repositoryRoot = this.CreateRepository("valid parent", false);
            string invalidOverride = Path.Combine(repositoryRoot, "invalid override");
            Directory.CreateDirectory(invalidOverride);

            InvalidOperationException failure = AssertInvalid(
                () => TestRepositoryRootResolver.Resolve(repositoryRoot, invalidOverride));
            StringAssert.Contains(failure.Message, "Explicit AORebirth repository root is invalid");
            StringAssert.Contains(failure.Message, Path.GetFullPath(invalidOverride));
        }

        [TestMethod]
        public void MissingRepositoryRootReportsStartingPathAndExpectedSentinels()
        {
            string start = Path.Combine(this.temporaryRoot, "missing", "nested");
            Directory.CreateDirectory(start);

            InvalidOperationException failure = AssertInvalid(
                () => TestRepositoryRootResolver.Resolve(start));
            StringAssert.Contains(failure.Message, Path.GetFullPath(start));
            StringAssert.Contains(failure.Message, "AI_START_HERE.md");
            StringAssert.Contains(failure.Message, "AORebirth/Server/ZoneEngine");
        }

        [TestMethod]
        public void GitMarkerWithoutRepositorySentinelsIsRejected()
        {
            string invalidRoot = Path.Combine(this.temporaryRoot, "git marker only");
            Directory.CreateDirectory(Path.Combine(invalidRoot, ".git"));

            Assert.IsFalse(TestRepositoryRootResolver.IsRepositoryRoot(invalidRoot));
            AssertInvalid(() => TestRepositoryRootResolver.Resolve(invalidRoot));
        }

        [TestMethod]
        public void RepeatedResolutionReturnsOneNormalizedRoot()
        {
            string repositoryRoot = this.CreateRepository("deterministic checkout", true);
            string nested = Path.Combine(repositoryRoot, "a", "b", "c");
            Directory.CreateDirectory(nested);

            string first = TestRepositoryRootResolver.Resolve(nested + Path.DirectorySeparatorChar);
            string second = TestRepositoryRootResolver.Resolve(nested);

            Assert.AreEqual(Path.GetFullPath(repositoryRoot), first);
            Assert.AreEqual(first, second);
            Assert.IsFalse(first.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal));
        }

        [TestMethod]
        public void CallerFilePathResolvesTheRealAcceptanceWorktree()
        {
            string repositoryRoot = TestRepositoryRootResolver.FindFromCallerFilePath();

            Assert.IsTrue(TestRepositoryRootResolver.IsRepositoryRoot(repositoryRoot));
            Assert.AreEqual(
                Path.GetFullPath(repositoryRoot),
                TestRepositoryRootResolver.Resolve(repositoryRoot));
        }

        private static InvalidOperationException AssertInvalid(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException exception)
            {
                return exception;
            }

            Assert.Fail("Expected InvalidOperationException.");
            return null;
        }

        private string CreateRepository(string name, bool linkedWorktree)
        {
            string repositoryRoot = Path.Combine(this.temporaryRoot, name);
            Directory.CreateDirectory(repositoryRoot);
            if (linkedWorktree)
            {
                File.WriteAllText(Path.Combine(repositoryRoot, ".git"), "gitdir: test-only");
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(repositoryRoot, ".git"));
            }

            File.WriteAllText(Path.Combine(repositoryRoot, "AI_START_HERE.md"), "test sentinel");
            File.WriteAllText(Path.Combine(repositoryRoot, "AGENTS.md"), "test sentinel");
            Directory.CreateDirectory(
                Path.Combine(repositoryRoot, "AORebirth", "Server", "ZoneEngine"));
            Directory.CreateDirectory(Path.Combine(repositoryRoot, "docs", "ai"));
            File.WriteAllText(
                Path.Combine(repositoryRoot, "docs", "ai", "WORKFLOW.md"),
                "test sentinel");
            return repositoryRoot;
        }
    }
}
