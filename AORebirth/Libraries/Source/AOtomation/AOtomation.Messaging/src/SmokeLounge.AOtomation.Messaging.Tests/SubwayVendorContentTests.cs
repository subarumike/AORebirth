// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System.Collections.Generic;
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
    }
}
