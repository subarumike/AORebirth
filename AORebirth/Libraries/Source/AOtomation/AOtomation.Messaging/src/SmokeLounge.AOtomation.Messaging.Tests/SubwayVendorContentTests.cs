// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine.Core.Playfields;

    [TestClass]
    public class SubwayVendorContentTests
    {
        [TestMethod]
        public void CaptureDefinesSixNpcOwnersAndSixResolvedShopEndpoints()
        {
            CapturedSubwayVendorDefinition[] definitions =
                CapturedSubwayVendorContentProvider.Definitions.ToArray();

            Assert.AreEqual(6, definitions.Length);
            CollectionAssert.AreEqual(
                new[]
                {
                    0x79135F51,
                    0x79135F52,
                    0x79135F53,
                    0x79135F54,
                    0x79135F55,
                    0x79135F56
                },
                definitions.Select(definition => definition.SourceNpcInstance).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    0x12ECC394,
                    0x12ECC395,
                    0x12ECC396,
                    0x12ECC397,
                    0x12ECC398,
                    0x12ECC399
                },
                definitions.Select(definition => definition.SourceVendorInstance).ToArray());
            CollectionAssert.AreEqual(
                new[] { 99637, 99572, 99570, 99574, 99601, 99634 },
                definitions.Select(definition => definition.VendorTemplateId).ToArray());

            CapturedSubwayVendorDefinition supplier = definitions.Single(
                definition => definition.DisplayName == "Container Supplier");
            Assert.IsTrue(supplier.HasCapturedStock);
            Assert.AreEqual(62, supplier.Stock.Count);
            Assert.AreEqual(
                6,
                definitions.Count(definition => definition.HasCapturedStock),
                "Exact template-99634 stock evidence must resolve the Container Supplier endpoint.");
        }

        [TestMethod]
        public void CapturedShopStocksPreserveAll202RowsAndContiguousSlots()
        {
            var expectedCounts =
                new Dictionary<string, int>
                {
                    { "Tailor", 21 },
                    { "Basic Quality Weaponsdealer", 31 },
                    { "Basic Quality Armorer", 29 },
                    { "Basic Quality Pharmacist", 40 },
                    { "Basic Tools Merchant", 19 },
                    { "Container Supplier", 62 }
                };

            CapturedSubwayVendorDefinition[] stocked = CapturedSubwayVendorContentProvider.Definitions
                .Where(definition => definition.HasCapturedStock)
                .ToArray();
            Assert.AreEqual(202, stocked.Sum(definition => definition.Stock.Count));
            foreach (CapturedSubwayVendorDefinition definition in stocked)
            {
                Assert.AreEqual(expectedCounts[definition.DisplayName], definition.Stock.Count);
                CollectionAssert.AreEqual(
                    Enumerable.Range(0, definition.Stock.Count).ToArray(),
                    definition.Stock.Select(stock => stock.Slot).ToArray(),
                    definition.DisplayName + " stock slots must retain capture order.");
            }
        }

        [TestMethod]
        public void CapturedShopStockFingerprintMatchesAuthoritativeCsv()
        {
            string canonical = string.Concat(
                CapturedSubwayVendorContentProvider.Definitions
                    .Where(definition => definition.HasCapturedStock)
                    .OrderBy(definition => definition.SourceVendorInstance)
                    .SelectMany(
                        definition => definition.Stock
                            .OrderBy(stock => stock.Slot)
                            .Select(
                                stock => string.Format(
                                    "{0:X8}:{1}:{2}:{3}:{4};",
                                    definition.SourceVendorInstance,
                                    stock.Slot,
                                    stock.LowId,
                                    stock.HighId,
                                    stock.Quality))));

            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }

            string actual = string.Concat(digest.Select(value => value.ToString("x2")));
            Assert.AreEqual(
                "df02869ae481758d371dc23c9a4f5f11734d7aae97648f4b2e040de2daa21507",
                actual);
        }

        [TestMethod]
        public void AlternateCapturedShopSnapshotIsAtomicAndMatchesAuthoritativeCsv()
        {
            CapturedSubwayVendorStockSnapshot snapshot =
                CapturedSubwayVendorContentProvider.EvidenceStockSnapshots.Single(
                    candidate => candidate.SnapshotId
                        == CapturedSubwayVendorContentProvider.AlternateStockSnapshotId);

            Assert.AreEqual(CapturedSubwayVendorContentProvider.SubwayPlayfieldResource, snapshot.PlayfieldResource);
            Assert.AreEqual(6, snapshot.Entries.Count);
            Assert.AreEqual(203, snapshot.TotalRows);
            CollectionAssert.AreEqual(
                new[] { 22, 31, 29, 40, 19, 62 },
                snapshot.Entries.Select(entry => entry.Stock.Count).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    0x12ECC394,
                    0x12ECC395,
                    0x12ECC396,
                    0x12ECC397,
                    0x12ECC398,
                    0x12ECC399
                },
                snapshot.Entries.Select(entry => entry.SourceVendorInstance).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    0x79135F51,
                    0x79135F52,
                    0x79135F53,
                    0x79135F54,
                    0x79135F55,
                    0x79135F56
                },
                snapshot.Entries.Select(entry => entry.SourceNpcInstance).ToArray());

            foreach (CapturedSubwayVendorStockSnapshotEntry entry in snapshot.Entries)
            {
                CollectionAssert.AreEqual(
                    Enumerable.Range(0, entry.Stock.Count).ToArray(),
                    entry.Stock.Select(stock => stock.Slot).ToArray(),
                    entry.SourceVendorInstance.ToString("X8") + " must retain one complete captured ordering.");
            }

            CollectionAssert.AreEqual(
                ReadAuthoritativeAlternateStockRows(),
                SnapshotRows(snapshot));
        }

        [TestMethod]
        public void AlternateCapturedSnapshotDoesNotReplaceCanonicalRuntimeStock()
        {
            CapturedSubwayVendorStockSnapshot baseline =
                CapturedSubwayVendorContentProvider.EvidenceStockSnapshots.Single(
                    candidate => candidate.SnapshotId
                        == CapturedSubwayVendorContentProvider.BaselineStockSnapshotId);
            foreach (CapturedSubwayVendorDefinition definition
                in CapturedSubwayVendorContentProvider.Definitions)
            {
                CapturedSubwayVendorStockSnapshotEntry entry = baseline.Entries.Single(
                    candidate => candidate.SourceVendorInstance
                        == definition.SourceVendorInstance);
                Assert.AreSame(definition.Stock, entry.Stock);
            }
        }

        [TestMethod]
        public void IdenticalPharmacistAndContainerObservationsReuseCanonicalStocks()
        {
            CapturedSubwayVendorStockSnapshot baseline = CapturedSubwayVendorContentProvider.EvidenceStockSnapshots[0];
            CapturedSubwayVendorStockSnapshot alternate = CapturedSubwayVendorContentProvider.EvidenceStockSnapshots[1];

            int[] duplicateDefinitionIndexes = { 3, 5 };
            foreach (int definitionIndex in duplicateDefinitionIndexes)
            {
                CapturedSubwayVendorStockSnapshotEntry baselineEntry =
                    baseline.Entries[definitionIndex];
                CapturedSubwayVendorStockSnapshotEntry alternateEntry =
                    alternate.Entries[definitionIndex];
                Assert.AreSame(baselineEntry.Stock, alternateEntry.Stock);
            }

            int[] changedDefinitionIndexes = { 0, 1, 2, 4 };
            foreach (int definitionIndex in changedDefinitionIndexes)
            {
                CapturedSubwayVendorStockSnapshotEntry baselineEntry =
                    baseline.Entries[definitionIndex];
                CapturedSubwayVendorStockSnapshotEntry alternateEntry =
                    alternate.Entries[definitionIndex];
                Assert.AreNotSame(baselineEntry.Stock, alternateEntry.Stock);
            }
        }

        [TestMethod]
        public void CapturedSnapshotResolutionFailsClosedOutsideExactEvidence()
        {
            CapturedSubwayVendorStockSnapshot snapshot =
                CapturedSubwayVendorContentProvider.EvidenceStockSnapshots.Single(
                    candidate => candidate.SnapshotId
                        == CapturedSubwayVendorContentProvider.AlternateStockSnapshotId);

            CapturedSubwayVendorStockSnapshotEntry entry = snapshot.Entries[0];
            ReadOnlyCollection<CapturedSubwayVendorStockDefinition> stock;
            Assert.IsFalse(
                snapshot.TryGetStock(
                    128,
                    entry.SourceNpcInstance,
                    entry.SourceVendorInstance,
                    entry.VendorTemplateId,
                    out stock));
            Assert.IsNull(stock);
            Assert.IsFalse(
                snapshot.TryGetStock(
                    127,
                    entry.SourceNpcInstance + 1,
                    entry.SourceVendorInstance,
                    entry.VendorTemplateId,
                    out stock));
            Assert.IsNull(stock);
            Assert.IsFalse(
                snapshot.TryGetStock(
                    127,
                    0x79775804,
                    0x12F6284F,
                    entry.VendorTemplateId,
                    out stock),
                "Capture-session identities must not become canonical runtime selectors.");
            Assert.IsNull(stock);
            Assert.IsFalse(
                snapshot.TryGetStock(
                    127,
                    entry.SourceNpcInstance,
                    int.MaxValue,
                    entry.VendorTemplateId,
                    out stock));
            Assert.IsNull(stock);
            Assert.IsFalse(
                snapshot.TryGetStock(
                    127,
                    entry.SourceNpcInstance,
                    entry.SourceVendorInstance,
                    entry.VendorTemplateId + 1,
                    out stock));
            Assert.IsNull(stock);
            Assert.IsTrue(
                snapshot.TryGetStock(
                    127,
                    entry.SourceNpcInstance,
                    entry.SourceVendorInstance,
                    entry.VendorTemplateId,
                    out stock));
            Assert.AreSame(entry.Stock, stock);
        }

        [TestMethod]
        public void MerchantAppearanceMetadataRemainsCaptureExact()
        {
            CapturedSubwayVendorDefinition tailor = CapturedSubwayVendorContentProvider.Definitions[0];
            CapturedSubwayVendorDefinition pharmacist = CapturedSubwayVendorContentProvider.Definitions[3];
            CapturedSubwayVendorDefinition supplier = CapturedSubwayVendorContentProvider.Definitions[5];

            Assert.AreEqual(1832, tailor.AppearanceValue);
            Assert.AreEqual(26076, tailor.MonsterData);
            Assert.AreEqual(40635, tailor.HeadMesh);
            Assert.AreEqual(28, tailor.CapturedScfuUnknown1.Count);
            Assert.AreEqual(1, pharmacist.Waypoints.Count);
            Assert.AreEqual(1640, pharmacist.AppearanceValue);
            Assert.AreEqual(26082, supplier.MonsterData);
            Assert.AreEqual(40634, supplier.HeadMesh);
            Assert.AreEqual("AOSharpLiveCapture/20260709-212115", supplier.Evidence);
            Assert.AreEqual(
                "AOSharpLiveCapture/20260613-221619;"
                + "identity=VendingMachine:C0000317;template=99634;slots=62;"
                + "exact-template-reuse",
                supplier.StockEvidence);
        }

        [TestMethod]
        public void TailorMeasurementChoicesMapToEightCapturedQlOneItems()
        {
            int[] actual = Enumerable.Range(0, 8)
                .Select(
                    index =>
                    {
                        int itemId;
                        Assert.IsTrue(CapturedSubwayTailorDialogueContent.TryGetMeasurementItemId(index, out itemId));
                        return itemId;
                    })
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { 256415, 256416, 256417, 256418, 256419, 256420, 256421, 256422 },
                actual);

            int invalidItemId;
            Assert.IsFalse(CapturedSubwayTailorDialogueContent.TryGetMeasurementItemId(-1, out invalidItemId));
            Assert.IsFalse(CapturedSubwayTailorDialogueContent.TryGetMeasurementItemId(8, out invalidItemId));
        }

        [TestMethod]
        public void TailorFirstOpenAndReopenResolveToCapturedGreetingNodes()
        {
            Assert.AreEqual(
                "tailor_root",
                CapturedSubwayTailorDialogueContent.ResolveRootNodeId(false));
            Assert.AreEqual(
                "tailor_root_reopen",
                CapturedSubwayTailorDialogueContent.ResolveRootNodeId(true));
        }

        private static string Fingerprint(CapturedSubwayVendorStockSnapshot snapshot)
        {
            string canonical = string.Concat(
                snapshot.Entries
                    .OrderBy(entry => entry.SourceVendorInstance)
                    .SelectMany(
                        entry => entry.Stock
                            .OrderBy(stock => stock.Slot)
                            .Select(
                                stock => string.Format(
                                    "{0:X8}:{1}:{2}:{3}:{4};",
                                    entry.SourceVendorInstance,
                                    stock.Slot,
                                    stock.LowId,
                                    stock.HighId,
                                    stock.Quality))));

            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }

            return string.Concat(digest.Select(value => value.ToString("x2")));
        }

        private static string[] SnapshotRows(
            CapturedSubwayVendorStockSnapshot snapshot)
        {
            return snapshot.Entries
                .SelectMany(
                    entry => entry.Stock.Select(
                        stock => string.Format(
                            "{0:X8}:{1}:{2}:{3}:{4}",
                            entry.SourceVendorInstance,
                            stock.Slot,
                            stock.LowId,
                            stock.HighId,
                            stock.Quality)))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ReadAuthoritativeAlternateStockRows()
        {
            var canonicalTerminalByCapturedTerminal =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "12F6284F", "12ECC394" },
                    { "12F62850", "12ECC395" },
                    { "12F62851", "12ECC396" },
                    { "12F62852", "12ECC397" },
                    { "12F62853", "12ECC398" },
                    { "12F62854", "12ECC399" }
                };
            return File.ReadAllLines(
                    RepositoryPath(
                        @"docs\evidence\data\subway-vendors-20260719-021611.csv"))
                .Skip(1)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(
                    value =>
                    {
                        string[] fields = value.Split(',');
                        Assert.AreEqual(8, fields.Length, value);
                        string identity = fields[3].Trim('"');
                        int separator = identity.IndexOf(':');
                        Assert.IsTrue(separator > 0, identity);
                        string terminal = identity
                            .Substring(separator + 1)
                            .TrimEnd(')')
                            .ToUpperInvariant();
                        string canonicalTerminal;
                        Assert.IsTrue(
                            canonicalTerminalByCapturedTerminal.TryGetValue(
                                terminal,
                                out canonicalTerminal),
                            terminal);
                        return string.Format(
                            "{0}:{1}:{2}:{3}:{4}",
                            canonicalTerminal,
                            fields[4],
                            fields[5],
                            fields[6],
                            fields[7]);
                    })
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string RepositoryPath(string relativePath)
        {
            DirectoryInfo cursor = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (cursor != null)
            {
                string candidate = Path.Combine(cursor.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                cursor = cursor.Parent;
            }

            Assert.Fail("Repository file was not found: " + relativePath);
            return string.Empty;
        }
    }
}
