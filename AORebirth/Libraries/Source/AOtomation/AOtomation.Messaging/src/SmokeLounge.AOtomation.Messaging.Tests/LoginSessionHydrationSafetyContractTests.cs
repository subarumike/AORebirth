namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class LoginSessionHydrationSafetyContractTests
    {
        [TestMethod]
        public void InventoryHydrationStateDistinguishesLoadedEmptyFromFailed()
        {
            string pageInterface = ReadRepositoryFile(@"AORebirth\Libraries\Source\AORebirth.Core\Inventory\IInventoryPage.cs");
            string page = ReadRepositoryFile(@"AORebirth\Libraries\Source\AORebirth.Core\Inventory\BaseInventoryPage.cs");
            string pages = ReadRepositoryFile(@"AORebirth\Libraries\Source\AORebirth.Core\Inventory\BaseInventoryPages.cs");

            StringAssert.Contains(pageInterface, "public enum InventoryHydrationState");
            StringAssert.Contains(pageInterface, "NotLoaded = 0");
            StringAssert.Contains(pageInterface, "Loading = 1");
            StringAssert.Contains(pageInterface, "Hydrated = 2");
            StringAssert.Contains(pageInterface, "Failed = 3");

            StringAssert.Contains(page, "var hydratedContent = new Dictionary<int, IItem>();");
            StringAssert.Contains(page, "this.HydrationState = InventoryHydrationState.Loading;");
            AssertTextBefore(page, "this.Content.Clear();", "this.MarkHydrated();");
            StringAssert.Contains(page, "this.MarkHydrationFailed();");
            StringAssert.Contains(pages, "public void MarkHydrated()");
            StringAssert.Contains(pages, "public void MarkHydrationFailed()");
        }

        [TestMethod]
        public void InventoryPersistenceFailsClosedBeforeDestructiveRewrite()
        {
            string page = ReadRepositoryFile(@"AORebirth\Libraries\Source\AORebirth.Core\Inventory\BaseInventoryPage.cs");
            string pages = ReadRepositoryFile(@"AORebirth\Libraries\Source\AORebirth.Core\Inventory\BaseInventoryPages.cs");
            string character = ReadRepositoryFile(@"AORebirth\Libraries\Source\AORebirth.Core\Entities\Character.cs");
            string pageWrite = page.Substring(page.IndexOf("public virtual bool Write()", StringComparison.Ordinal));
            string pagesWrite = pages.Substring(pages.IndexOf("public bool Write()", StringComparison.Ordinal));

            AssertTextBefore(pageWrite, "if (!this.IsHydrated)", "ItemDao.Instance.Delete(");
            AssertTextBefore(pageWrite, "if (!this.IsHydrated)", "InstancedItemDao.Instance.Delete(");
            StringAssert.Contains(pagesWrite, "if (!this.IsHydrated)");
            StringAssert.Contains(character, "if (this.BaseInventory != null && !this.BaseInventory.Write())");
            StringAssert.Contains(character, "return false;");
        }

        [TestMethod]
        public void GmiMissingOptionalSchemaSkipsLoginPendingWithdrawalProcessing()
        {
            string dao = ReadRepositoryFile(@"AORebirth\Libraries\Source\AORebirth.Database\Dao\GmiVaultDao.cs");
            string runtime = ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\GMI\GmiRuntimeService.cs");

            StringAssert.Contains(dao, "public static bool CanUseVaultSchema()");
            StringAssert.Contains(dao, "information_schema.tables");
            StringAssert.Contains(dao, "gmi_vault");
            StringAssert.Contains(dao, "gmi_vault_item");
            StringAssert.Contains(dao, "IsMissingOptionalGmiTable");
            StringAssert.Contains(dao, "throw;");

            AssertTextBefore(runtime, "if (!GmiVaultDao.CanUseVaultSchema())", "GmiVault vault = GetOrCreate(character);");
            StringAssert.Contains(runtime, "failureReason = \"Market vault unavailable.\";");
        }

        [TestMethod]
        public void ClientConnectedUsesTransferAwareMasterSessionSemantics()
        {
            string connected = ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs");

            StringAssert.Contains(connected, "CombatXpRuntimeService.IsPlayfieldTransferLogin(client)");
            StringAssert.Contains(connected, "CombatXpRuntimeService.PrepareXpStatsForLogin(");
            StringAssert.Contains(connected, "isPlayfieldTransfer");
            StringAssert.Contains(connected, "transferDynel.IsTeleporting = false;");
            StringAssert.Contains(connected, "InitializeActionableState(client);");
            StringAssert.Contains(connected, "SendActionableState(client);");
            AssertTextBefore(connected, "InitializeActionableState(client);", "SendActionableState(client);");
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            string root = FindRepositoryRoot();
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "AORebirth"))
                    && File.Exists(Path.Combine(dir.FullName, "AI_START_HERE.md")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate AORebirth repository root.");
        }

        private static void AssertTextBefore(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);
            Assert.IsTrue(beforeIndex >= 0, "Missing expected text: " + before);
            Assert.IsTrue(afterIndex >= 0, "Missing expected text: " + after);
            Assert.IsTrue(beforeIndex < afterIndex, "Expected text order: " + before + " before " + after);
        }
    }
}
