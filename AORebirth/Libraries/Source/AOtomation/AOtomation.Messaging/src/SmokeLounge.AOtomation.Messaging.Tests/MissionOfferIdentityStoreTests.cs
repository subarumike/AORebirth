namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Missions;

    [TestClass]
    public class MissionOfferIdentityStoreTests
    {
        private const string Header = "AORebirth-MissionOfferIdentityCursor";

        private string temporaryRoot;

        [TestInitialize]
        public void Initialize()
        {
            this.temporaryRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "aorebirth-mission-offer-identity-tests-"
                    + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.temporaryRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.temporaryRoot))
            {
                Directory.Delete(this.temporaryRoot, true);
            }
        }

        [TestMethod]
        public void AllocationSurvivesRestartAndContinuesAfterDurableCursor()
        {
            MissionOfferIdentityStore firstStore = this.CreateStore("restart");
            MissionOfferIdentityAllocationResult first =
                firstStore.TryAllocate(delegate { return false; });
            Assert.IsTrue(first.Succeeded, first.Diagnostic);
            Assert.AreEqual(MissionOfferIdentityStore.MinimumOfferId, first.OfferId);

            MissionOfferIdentityStore restartedStore = this.CreateStore("restart");
            MissionOfferIdentityLoadResult restored = restartedStore.Load();
            Assert.IsTrue(restored.IsValid, restored.Diagnostic);
            Assert.IsTrue(restored.StateExists);
            Assert.AreEqual(first.OfferId, restored.LastAllocatedOfferId);

            MissionOfferIdentityAllocationResult second =
                restartedStore.TryAllocate(delegate { return false; });
            Assert.IsTrue(second.Succeeded, second.Diagnostic);
            Assert.AreEqual(first.OfferId + 1, second.OfferId);
            Assert.AreEqual(
                second.OfferId,
                this.CreateStore("restart").Load().LastAllocatedOfferId);
        }

        [TestMethod]
        public void AllocationSkipsEveryReportedCollisionWithoutPublishingIt()
        {
            MissionOfferIdentityStore store = this.CreateStore("collision");
            var inspected = new List<int>();
            MissionOfferIdentityAllocationResult result =
                store.TryAllocate(
                    delegate(int candidate)
                    {
                        inspected.Add(candidate);
                        return candidate
                               < MissionOfferIdentityStore.MinimumOfferId + 2;
                    });

            Assert.IsTrue(result.Succeeded, result.Diagnostic);
            Assert.AreEqual(
                MissionOfferIdentityStore.MinimumOfferId + 2,
                result.OfferId);
            CollectionAssert.AreEqual(
                new[]
                {
                    MissionOfferIdentityStore.MinimumOfferId,
                    MissionOfferIdentityStore.MinimumOfferId + 1,
                    MissionOfferIdentityStore.MinimumOfferId + 2
                },
                inspected);
            Assert.AreEqual(result.OfferId, store.Load().LastAllocatedOfferId);
        }

        [TestMethod]
        public void MalformedUnknownVersionAndHashMismatchFailClosed()
        {
            MissionOfferIdentityStore malformedStore = this.CreateStore("malformed");
            Directory.CreateDirectory(malformedStore.DirectoryPath);
            File.WriteAllText(
                malformedStore.StatePath,
                Header + "\r\nFormatVersion=1\r\n",
                new UTF8Encoding(false));
            AssertLoadFailure(malformedStore, "malformed/truncated");

            MissionOfferIdentityStore versionStore = this.CreateStore("version");
            Assert.IsTrue(
                versionStore.TryAllocate(delegate { return false; }).Succeeded);
            RewriteField(versionStore.StatePath, "FormatVersion", "999", true);
            AssertLoadFailure(versionStore, "Unknown offer identity state version");

            MissionOfferIdentityStore hashStore = this.CreateStore("hash");
            Assert.IsTrue(
                hashStore.TryAllocate(delegate { return false; }).Succeeded);
            RewriteField(
                hashStore.StatePath,
                "LastAllocatedOfferId",
                (MissionOfferIdentityStore.MinimumOfferId + 7).ToString(
                    CultureInfo.InvariantCulture),
                false);
            AssertLoadFailure(hashStore, "SHA-256 mismatch");
        }

        [TestMethod]
        public void CollisionValidationFailureDoesNotAdvanceDurableCursor()
        {
            MissionOfferIdentityStore store = this.CreateStore("predicate-failure");
            MissionOfferIdentityAllocationResult first =
                store.TryAllocate(delegate { return false; });
            Assert.IsTrue(first.Succeeded, first.Diagnostic);

            MissionOfferIdentityAllocationResult failed =
                store.TryAllocate(
                    delegate
                    {
                        throw new InvalidOperationException("collision owner unavailable");
                    });
            Assert.IsFalse(failed.Succeeded);
            StringAssert.Contains(
                failed.Diagnostic,
                "Offer identity collision validation failed");
            Assert.AreEqual(first.OfferId, store.Load().LastAllocatedOfferId);
        }

        [TestMethod]
        public void ConcurrentStoresPublishDistinctIdsAndLeaveOnlyCompleteState()
        {
            MissionOfferIdentityStore firstStore = this.CreateStore("concurrent");
            MissionOfferIdentityStore secondStore = this.CreateStore("concurrent");
            var start = new ManualResetEventSlim(false);

            Task<MissionOfferIdentityAllocationResult> firstTask =
                Task.Factory.StartNew(
                    delegate
                    {
                        start.Wait();
                        return firstStore.TryAllocate(delegate { return false; });
                    });
            Task<MissionOfferIdentityAllocationResult> secondTask =
                Task.Factory.StartNew(
                    delegate
                    {
                        start.Wait();
                        return secondStore.TryAllocate(delegate { return false; });
                    });

            start.Set();
            Task.WaitAll(firstTask, secondTask);
            Assert.IsTrue(firstTask.Result.Succeeded, firstTask.Result.Diagnostic);
            Assert.IsTrue(secondTask.Result.Succeeded, secondTask.Result.Diagnostic);
            Assert.AreNotEqual(firstTask.Result.OfferId, secondTask.Result.OfferId);

            var allocated = new List<int>
            {
                firstTask.Result.OfferId,
                secondTask.Result.OfferId
            };
            allocated.Sort();
            CollectionAssert.AreEqual(
                new[]
                {
                    MissionOfferIdentityStore.MinimumOfferId,
                    MissionOfferIdentityStore.MinimumOfferId + 1
                },
                allocated);

            MissionOfferIdentityLoadResult loaded = firstStore.Load();
            Assert.IsTrue(loaded.IsValid, loaded.Diagnostic);
            Assert.AreEqual(
                MissionOfferIdentityStore.MinimumOfferId + 1,
                loaded.LastAllocatedOfferId);
            Assert.AreEqual(
                0,
                Directory.GetFiles(firstStore.DirectoryPath, "*.tmp").Length);
            Assert.AreEqual(
                0,
                Directory.GetFiles(firstStore.DirectoryPath, "*.bak").Length);
            AssertCanonicalState(firstStore.StatePath, loaded.LastAllocatedOfferId);
        }

        private MissionOfferIdentityStore CreateStore(string name)
        {
            return new MissionOfferIdentityStore(
                Path.Combine(this.temporaryRoot, name, "mission-state"));
        }

        private static void AssertLoadFailure(
            MissionOfferIdentityStore store,
            string expectedDiagnostic)
        {
            MissionOfferIdentityLoadResult loaded = store.Load();
            Assert.IsFalse(loaded.IsValid);
            Assert.IsTrue(loaded.StateExists);
            StringAssert.Contains(loaded.Diagnostic, expectedDiagnostic);

            MissionOfferIdentityAllocationResult allocation =
                store.TryAllocate(delegate { return false; });
            Assert.IsFalse(allocation.Succeeded);
            StringAssert.Contains(allocation.Diagnostic, expectedDiagnostic);
        }

        private static void AssertCanonicalState(string path, int expectedOfferId)
        {
            string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
            Assert.AreEqual(4, lines.Length);
            Assert.AreEqual(Header, lines[0]);
            Assert.AreEqual("FormatVersion=1", lines[1]);
            Assert.AreEqual(
                "LastAllocatedOfferId="
                + expectedOfferId.ToString(CultureInfo.InvariantCulture),
                lines[2]);

            string canonical = lines[1] + "\r\n" + lines[2] + "\r\n";
            Assert.AreEqual(
                "RecordSha256=" + ComputeSha256(canonical),
                lines[3]);
        }

        private static void RewriteField(
            string path,
            string key,
            string value,
            bool recomputeHash)
        {
            string[] lines = File.ReadAllLines(path);
            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            string suppliedHash = string.Empty;
            for (int i = 1; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string field = lines[i].Substring(0, separator);
                string fieldValue = lines[i].Substring(separator + 1);
                if (field == "RecordSha256")
                {
                    suppliedHash = fieldValue;
                }
                else
                {
                    fields[field] = field == key ? value : fieldValue;
                }
            }

            var canonical = new StringBuilder();
            foreach (KeyValuePair<string, string> field in fields)
            {
                canonical.Append(field.Key);
                canonical.Append('=');
                canonical.Append(field.Value);
                canonical.Append("\r\n");
            }

            string hash = recomputeHash
                              ? ComputeSha256(canonical.ToString())
                              : suppliedHash;
            File.WriteAllText(
                path,
                Header
                + "\r\n"
                + canonical
                + "RecordSha256="
                + hash
                + "\r\n",
                new UTF8Encoding(false));
        }

        private static string ComputeSha256(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(bytes);
            }

            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
