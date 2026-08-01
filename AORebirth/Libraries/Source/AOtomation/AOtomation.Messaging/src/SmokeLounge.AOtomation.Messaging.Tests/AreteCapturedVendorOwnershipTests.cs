namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AreteCapturedVendorOwnershipTests
    {
        private static readonly string[] VendorNames =
            {
                "AntonioStacklund",
                "RemiGallois",
                "SarahGreene"
            };

        [TestMethod]
        public void CapturedVendorStacksAreCompiledAndOwnedByAretePlayfieldLifecycle()
        {
            string project = ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\ZoneEngine.csproj");
            string owner = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldVendorRuntimeService.cs");

            foreach (string vendorName in VendorNames)
            {
                AssertContains(
                    project,
                    "Core\\Playfields\\CapturedArete" + vendorName + "VendorContentProvider.cs");
                AssertContains(
                    project,
                    "Core\\Playfields\\CapturedArete" + vendorName + "VendorRuntimeRegistry.cs");
                AssertContains(
                    project,
                    "Core\\Playfields\\CapturedArete" + vendorName + "VendorRuntimeService.cs");
                AssertContains(
                    project,
                    "Core\\MessageHandlers\\CapturedArete" + vendorName + "VendorInteractionHandler.cs");
            }

            AssertInOrder(
                owner,
                "this.capturedAreteAlexArea.Spawn(playfield, playfieldIdentity, dynelRegistry);",
                "this.AttachCapturedAreteMarcoSpidaVendor(playfield, playfieldIdentity, dynelRegistry);",
                "this.AttachCapturedAreteLoreleiVendor(playfield, playfieldIdentity, dynelRegistry);",
                "this.capturedAreteAntonio.Attach(playfield, playfieldIdentity, dynelRegistry);",
                "this.capturedAreteRemi.Attach(playfield, playfieldIdentity, dynelRegistry);",
                "this.capturedAreteSarah.Attach(playfield, playfieldIdentity, dynelRegistry);");
            AssertInOrder(
                owner,
                "this.capturedAreteSarah.Clear(playfieldIdentity, dynelRegistry);",
                "this.capturedAreteRemi.Clear(playfieldIdentity, dynelRegistry);",
                "this.capturedAreteAntonio.Clear(playfieldIdentity, dynelRegistry);",
                "this.capturedAreteLorelei.Clear(playfieldIdentity, dynelRegistry);",
                "this.capturedAreteMarcoSpida.Clear(playfieldIdentity, dynelRegistry);",
                "this.capturedAreteAlexArea.Clear(playfieldIdentity, dynelRegistry);");
        }

        [TestMethod]
        public void CapturedVendorStocksPreserveExactObservedOrderAndMetadata()
        {
            AssertProvider(
                "AntonioStacklund",
                "0x78E0FC7C",
                "0x12E7720D",
                248368,
                "AOSharpLiveCapture/20260726-Antonio-Stacklund",
                new[]
                    {
                        "0, 248306, 248306, 1", "1, 150922, 150922, 10",
                        "2, 121569, 121569, 1", "3, 248340, 248340, 1",
                        "4, 248338, 248338, 1", "5, 121567, 121567, 1",
                        "6, 121568, 121568, 1", "7, 121570, 121570, 1",
                        "8, 121571, 121571, 1", "9, 121564, 121564, 1",
                        "10, 218403, 218403, 1", "11, 248339, 248339, 1",
                        "12, 218395, 218395, 1", "13, 218406, 218406, 1",
                        "14, 121565, 121565, 1", "15, 218404, 218404, 1"
                    });
            AssertProvider(
                "RemiGallois",
                "0x78E0FC75",
                "0x12E7720C",
                99634,
                "AOSharpLiveCapture/20260727-213512",
                new[]
                    {
                        "0, 125219, 125219, 1", "1, 21605, 21605, 1",
                        "2, 21609, 21609, 1", "3, 21601, 21601, 1",
                        "4, 126757, 126757, 1", "5, 21613, 21613, 1",
                        "6, 295765, 295765, 1", "7, 160224, 160225, 2",
                        "8, 160224, 160225, 5", "9, 152154, 152155, 4",
                        "10, 152154, 152155, 9", "11, 122924, 122924, 1",
                        "12, 122924, 122925, 7", "13, 121969, 121969, 1",
                        "14, 121969, 121970, 6", "15, 122121, 122122, 2",
                        "16, 122121, 122122, 7", "17, 122425, 122425, 1",
                        "18, 122425, 122426, 9", "19, 123267, 123267, 10",
                        "20, 125043, 125043, 1", "21, 125043, 125044, 8",
                        "22, 122216, 122217, 3", "23, 122216, 122217, 5",
                        "24, 124910, 124911, 2", "25, 124910, 124911, 9",
                        "26, 209283, 209284, 5", "27, 209283, 209284, 8",
                        "28, 152339, 152340, 6", "29, 152339, 152340, 9",
                        "30, 124276, 124277, 4", "31, 124276, 124277, 7",
                        "32, 209269, 209270, 4", "33, 144101, 144102, 9",
                        "34, 142836, 142837, 9", "35, 142837, 142837, 10",
                        "36, 160288, 160288, 1", "37, 160288, 160289, 9"
                    });
            AssertProvider(
                "SarahGreene",
                "0x78E0FC69",
                "0x12E7720A",
                295748,
                "AOSharpLiveCapture/20260726-sara-greene-vendor",
                new[]
                    {
                        "0, 162294, 162294, 10", "1, 162293, 162293, 10",
                        "2, 162290, 162290, 10", "3, 162289, 162289, 10",
                        "4, 162292, 162292, 10", "5, 162291, 162291, 10",
                        "6, 248273, 248273, 1", "7, 248269, 248269, 1",
                        "8, 248277, 248277, 1", "9, 248271, 248271, 1",
                        "10, 248275, 248275, 1", "11, 234050, 234051, 11",
                        "12, 234061, 234062, 2", "13, 234061, 234062, 6",
                        "14, 234065, 234066, 13", "15, 234066, 234066, 15",
                        "16, 234057, 234058, 4", "17, 234057, 234058, 5",
                        "18, 234059, 234060, 8", "19, 234060, 234060, 15",
                        "20, 234063, 234064, 11", "21, 234063, 234064, 13"
                    });
        }

        [TestMethod]
        public void CapturedVendorAttachAndClearBindOnlyTheExactAreteNpcEndpoint()
        {
            foreach (string vendorName in VendorNames)
            {
                string service = ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedArete"
                    + vendorName
                    + "VendorRuntimeService.cs");
                string registry = ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedArete"
                    + vendorName
                    + "VendorRuntimeRegistry.cs");

                AssertContains(service, "playfieldIdentity.Instance != CapturedArete" + vendorName);
                AssertContains(service, ".SourceNpcInstance");
                AssertContains(service, ".DisplayName");
                AssertContains(service, "vendor.NpcIdentity = character.Identity;");
                AssertContains(service, "dynelRegistry.Register(vendor);");
                AssertContains(
                    service,
                    "Identity vendorIdentity = vendor == null ? Identity.None : vendor.Identity;");
                AssertContains(service, "RuntimeRegistry.Register(");
                AssertContains(service, "RuntimeRegistry.RemoveForPlayfield(playfieldIdentity);");
                AssertContains(service, "dynelRegistry.Unregister(vendor.Identity);");
                AssertContains(registry, "Entries[runtime.NpcIdentity.Instance] = runtime;");
                AssertContains(registry, "this.VendorIdentity = vendorIdentity;");
                AssertContains(registry, "RemoveForPlayfield");
            }
        }

        [TestMethod]
        public void CapturedVendorUseDispatchIsExactOrderedAndFailsClosed()
        {
            string dispatch = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\PlayfieldInteractionRuntimeService.cs");
            int genericFallback = dispatch.IndexOf("if (!generatedPlayfield", StringComparison.Ordinal);
            Assert.IsTrue(genericFallback >= 0);

            foreach (string vendorName in VendorNames)
            {
                string handlerCall =
                    "CapturedArete" + vendorName
                    + "VendorInteractionHandler.Default.TryHandleUse(client, message, target)";
                int handlerPosition = dispatch.IndexOf(handlerCall, StringComparison.Ordinal);
                Assert.IsTrue(handlerPosition >= 0, handlerCall);
                Assert.IsTrue(handlerPosition < genericFallback, handlerCall);
                Assert.AreEqual(1, CountOccurrences(dispatch, handlerCall), handlerCall);

                string handler = ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\CapturedArete"
                    + vendorName
                    + "VendorInteractionHandler.cs");
                AssertContains(handler, "npcIdentity.Type != IdentityType.CanbeAffected");
                AssertContains(handler, "RuntimeRegistry.TryGet(npcIdentity.Instance, out runtime)");
                AssertContains(handler, "RuntimeRegistry.Same(runtime.NpcIdentity, npcIdentity)");
                AssertContains(handler, "RuntimeRegistry.Same(");
                AssertContains(handler, "runtime.PlayfieldIdentity");
                AssertContains(handler, "runtime.VendorIdentity.Instance == 0");
                AssertContains(handler, "Pool.Instance.GetObject<Vendor>(");
                AssertInOrder(
                    handler,
                    "VendingMachineFullUpdateMessageHandler.Default.Send(character, vendor);",
                    "ShopUpdateMessageHandler.Default.Send(",
                    "TradeMessageHandler.Default.Send(character, temporaryBag);");
                Assert.IsFalse(
                    handler.Contains("AreteLandingPlayfieldId"),
                    vendorName + " must not use a broad playfield fallback for Use.");
                Assert.IsFalse(
                    handler.Contains("DisplayName"),
                    vendorName + " must not use a name fallback for Use.");
            }
        }

        private static void AssertProvider(
            string vendorName,
            string sourceNpcHex,
            string sourceVendorHex,
            int captureTemplate,
            string evidence,
            string[] expectedRows)
        {
            string source = ReadRepositoryFile(
                @"AORebirth\Server\ZoneEngine\Core\Playfields\CapturedArete"
                + vendorName
                + "VendorContentProvider.cs");
            AssertContains(source, "SourceNpcInstance = unchecked((int)" + sourceNpcHex + ")");
            AssertContains(source, "SourceVendorInstance = unchecked((int)" + sourceVendorHex + ")");
            AssertContains(source, "CaptureVendorTemplateId = " + captureTemplate);
            AssertContains(source, "Evidence = \"" + evidence + "\"");
            Assert.AreEqual(
                expectedRows.Length,
                Regex.Matches(source, "new CapturedAreteAlexAreaVendorStockDefinition\\(").Count,
                vendorName);

            int cursor = 0;
            foreach (string row in expectedRows)
            {
                string exact = "new CapturedAreteAlexAreaVendorStockDefinition(" + row + ")";
                int position = source.IndexOf(exact, cursor, StringComparison.Ordinal);
                Assert.IsTrue(position >= cursor, vendorName + " missing or out-of-order row " + row);
                cursor = position + exact.Length;
            }
        }

        private static void AssertInOrder(string source, params string[] values)
        {
            int cursor = 0;
            foreach (string value in values)
            {
                int position = source.IndexOf(value, cursor, StringComparison.Ordinal);
                Assert.IsTrue(position >= cursor, "Missing or out-of-order text: " + value);
                cursor = position + value.Length;
            }
        }

        private static void AssertContains(string source, string value)
        {
            Assert.IsTrue(source.Contains(value), "Missing text: " + value);
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
