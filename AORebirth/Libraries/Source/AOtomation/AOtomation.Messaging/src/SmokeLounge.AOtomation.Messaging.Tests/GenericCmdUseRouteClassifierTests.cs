// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.MessageHandlers;

    #endregion

    [TestClass]
    public class GenericCmdUseRouteClassifierTests
    {
        [TestMethod]
        public void CurrentRouteOrderMatchesGenericCmdUseBranchOrder()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    GenericCmdUseRoute.RexB18DBoxProgress,
                    GenericCmdUseRoute.InventoryItem,
                    GenericCmdUseRoute.WearOrSocialBackpack,
                    GenericCmdUseRoute.BackpackContainer,
                    GenericCmdUseRoute.PrivateCityGuestKeyGenerator,
                    GenericCmdUseRoute.PrivateCityController,
                    GenericCmdUseRoute.DirectCorpse,
                    GenericCmdUseRoute.DeadNpcCorpse,
                    GenericCmdUseRoute.CapturedGridTerminal,
                    GenericCmdUseRoute.GridEnterTerminal,
                    GenericCmdUseRoute.SurgeryClinic,
                    GenericCmdUseRoute.PoolOnUseOrTrade,
                    GenericCmdUseRoute.StatelFallback
                },
                GenericCmdUseRouteClassifier.CurrentRouteOrder);
        }



        [TestMethod]
        public void KnownPrivateCityTargetsSelectCurrentCapturedRoutes()
        {
            AssertRoute(
                GenericCmdUseRoute.PrivateCityGuestKeyGenerator,
                Terminal(GenericCmdUseRouteClassifier.RuntimePrivateCityGuestKeyTerminalInstance),
                isPrivateCityPlayfield: true);

            AssertRoute(
                GenericCmdUseRoute.PrivateCityGuestKeyGenerator,
                Terminal(GenericCmdUseRouteClassifier.CapturedPrivateCityGuestKeyTerminalInstance),
                isPrivateCityPlayfield: true);

            AssertRoute(
                GenericCmdUseRoute.StatelFallback,
                Terminal(GenericCmdUseRouteClassifier.RuntimePrivateCityGuestKeyTerminalInstance));

            AssertRoute(
                GenericCmdUseRoute.PrivateCityController,
                CityController(GenericCmdUseRouteClassifier.RuntimeCityControllerInstance));

            AssertRoute(
                GenericCmdUseRoute.PrivateCityController,
                CityController(GenericCmdUseRouteClassifier.CapturedCityControllerInstance));

            AssertRoute(
                GenericCmdUseRoute.PrivateCityController,
                CityController(GenericCmdUseRouteClassifier.CapturedNonOrgCityControllerInstance));
        }

        [TestMethod]
        public void GuestKeyGeneratorRulesExposeCurrentCapturedConstants()
        {
            Assert.AreEqual(
                unchecked((int)0x5751538B),
                GuestKeyGeneratorInteractionRules.CapturedPrivateCityGuestKeyTerminalInstance);
            Assert.AreEqual(
                unchecked((int)0x574B84AB),
                GuestKeyGeneratorInteractionRules.RuntimePrivateCityGuestKeyTerminalInstance);
            Assert.AreEqual(280642, GuestKeyGeneratorInteractionRules.CapturedCityAccessCardTemplateId);
            Assert.AreEqual(0x6F, GuestKeyGeneratorInteractionRules.CapturedCityAccessCardOverflowSlot);
            Assert.AreEqual(15 * 60 * 1000, GuestKeyGeneratorInteractionRules.CityAccessCardLifetimeMilliseconds);
            Assert.IsTrue(
                GuestKeyGeneratorInteractionRules.IsPrivateCityGuestKeyTerminalTarget(
                    Terminal(GuestKeyGeneratorInteractionRules.RuntimePrivateCityGuestKeyTerminalInstance)));
        }

        [TestMethod]
        public void CityControllerMenuModeMatchesCurrentOwnerAndLimitedRules()
        {
            Assert.AreEqual(
                CityControllerMenuMode.OwnerMember,
                CityControllerInteractionRules.ResolveMenuMode(1970177, 1970177));

            Assert.AreEqual(
                CityControllerMenuMode.NonOrgLimited,
                CityControllerInteractionRules.ResolveMenuMode(0, 1970177));

            Assert.AreEqual(
                CityControllerMenuMode.NonOrgLimited,
                CityControllerInteractionRules.ResolveMenuMode(1, 1970177));
        }

        [TestMethod]
        public void CorpseRoutesKeepDeadNpcFallbackBehindDirectCorpse()
        {
            AssertRoute(GenericCmdUseRoute.DirectCorpse, new Identity { Type = IdentityType.Corpse, Instance = 0x20 });

            AssertRoute(
                GenericCmdUseRoute.DeadNpcCorpse,
                new Identity { Type = IdentityType.CanbeAffected, Instance = 0x30 },
                deadNpcCorpseRouted: true);

            AssertRoute(
                GenericCmdUseRoute.StatelFallback,
                new Identity { Type = IdentityType.CanbeAffected, Instance = 0x30 });
        }

        [TestMethod]
        public void SubwayTeleportProxyOverridesPreserveOfficialEntryAndMainExitLandings()
        {
            string playfieldLoader =
                ReadRepositoryFile(@"AORebirth\Libraries\Source\PlayfieldLoader\PlayfieldLoader.cs");
            AssertContains(playfieldLoader, "private const int SubwayPlayfieldId = 127;");
            AssertContains(
                playfieldLoader,
                "private const int SubwayEntranceDestinationDoorInstance = unchecked((int)0xC006007F);");
            AssertContains(
                playfieldLoader,
                "if (foundproxyteleport && resolvedDestinationPlayfieldId == SubwayPlayfieldId)");
            AssertContains(playfieldLoader, "playfieldid = SubwayPlayfieldId;");
            AssertContains(
                playfieldLoader,
                "doorinstance = SubwayEntranceDestinationDoorInstance;");
            AssertContains(playfieldLoader, "ShouldSynthesizeReverseProxyExit(");
            AssertContains(playfieldLoader, "destinationPlayfieldId != SubwayPlayfieldId");
            AssertContains(
                playfieldLoader,
                "destinationDoorInstance == SubwayEntranceDestinationDoorInstance");
            Assert.IsFalse(
                playfieldLoader.Contains("RemoveSubwayNonEntranceDoorStatels"),
                "PF127 ordinary interior door statels must remain loaded.");
        }

        [TestMethod]
        public void CorpseInteractionRulesExposeCurrentRouteModeDecisions()
        {
            Assert.AreEqual(
                CorpseInteractionRouteMode.DirectCorpse,
                CorpseInteractionRules.ResolveRouteMode(
                    new Identity { Type = IdentityType.Corpse, Instance = 0x20 },
                    false));

            Assert.AreEqual(
                CorpseInteractionRouteMode.DeadNpcCorpse,
                CorpseInteractionRules.ResolveRouteMode(
                    new Identity { Type = IdentityType.CanbeAffected, Instance = 0x30 },
                    true));

            Assert.AreEqual(
                CorpseInteractionRouteMode.None,
                CorpseInteractionRules.ResolveRouteMode(
                    new Identity { Type = IdentityType.CanbeAffected, Instance = 0x30 },
                    false));

            Assert.AreEqual(550, CorpseInteractionRules.CorpseUseAcknowledgeDelayMilliseconds);
        }

        [TestMethod]
        public void OmniTrainingGroundCaptureKeepsCorpseOpenOrderingFixture()
        {
            string fixture = ReadRepositoryFile("docs\\generated\\omni_training_ground_capture_20260622_182442_reference.json");
            string report = ReadRepositoryFile("docs\\generated\\omni_training_ground_capture_20260622_182442_inventory.md");

            AssertContains(fixture, "\"emptyCorpseOpen\": [");
            AssertContains(fixture, "\"messageType\": \"GenericCmd\"");
            AssertContains(fixture, "\"target\": \"(Corpse:F6C003)\"");
            AssertContains(fixture, "\"inventoryIdentity\": \"(Corpse:F6C003)\"");
            AssertContains(fixture, "\"messageType\": \"InventoryUpdate\"");
            AssertContains(fixture, "\"sequence\": 122");
            AssertContains(fixture, "\"handle\": 112");
            AssertContains(fixture, "\"unknown1\": 21");
            AssertContains(fixture, "\"unknown2\": 2");
            AssertContains(fixture, "\"unknown3\": 1");
            AssertContains(fixture, "\"itemCount\": 0");
            AssertContains(fixture, "\"summary\": \"Action=Use Temp1=1 Count=5 Unknown=0\"");
            AssertTextBefore(fixture, "\"source\": \"events.log:239\"", "\"messageType\": \"InventoryUpdate\"");
            AssertTextBefore(fixture, "\"messageType\": \"InventoryUpdate\"", "\"summary\": \"Action=Use Temp1=1 Count=5 Unknown=0\"");

            AssertContains(fixture, "\"corpseInventoryWithItems\": {");
            AssertContains(fixture, "\"inventoryIdentity\": \"(Corpse:F6C009)\"");
            AssertContains(fixture, "\"sequence\": 2001");
            AssertContains(fixture, "\"handle\": 133");
            AssertContains(fixture, "\"lowId\": 201135");
            AssertContains(fixture, "\"highId\": 201136");
            AssertContains(fixture, "\"quality\": 14");

            AssertContains(report, "`OUT GenericCmd Use`");
            AssertContains(report, "`IN InventoryUpdate`");
            AssertContains(report, "`IN GenericCmd` success ack");
            AssertTextBefore(report, "`OUT GenericCmd Use`", "`IN InventoryUpdate`");
            AssertTextBefore(report, "`IN InventoryUpdate`", "`IN GenericCmd` success ack");
            AssertContains(report, "Treat this capture as corpse access/open/content evidence");
        }

        [TestMethod]
        public void GridAndSurgeryRoutesKeepCurrentPrecedence()
        {
            AssertRoute(
                GenericCmdUseRoute.CapturedGridTerminal,
                Terminal(GenericCmdUseRouteClassifier.CapturedBorealisGridTerminalInstance),
                capturedGridTerminalRouteMatched: true,
                gridEnterTerminalMatched: true,
                surgeryClinicTerminalMatched: true);

            AssertRoute(
                GenericCmdUseRoute.GridEnterTerminal,
                Terminal(0x01020304),
                gridEnterTerminalMatched: true,
                surgeryClinicTerminalMatched: true);

            AssertRoute(
                GenericCmdUseRoute.SurgeryClinic,
                Terminal(GenericCmdUseRouteClassifier.CapturedSurgeryClinicTerminalInstance),
                surgeryClinicTerminalMatched: true);
        }

        [TestMethod]
        public void GridTerminalRulesExposeCurrentRouteModePrecedence()
        {
            Assert.AreEqual(
                GridTerminalInteractionRouteMode.CapturedGridTerminal,
                GridTerminalInteractionRules.ResolveRouteMode(true, true));

            Assert.AreEqual(
                GridTerminalInteractionRouteMode.GridEnterTerminal,
                GridTerminalInteractionRules.ResolveRouteMode(false, true));

            Assert.AreEqual(
                GridTerminalInteractionRouteMode.None,
                GridTerminalInteractionRules.ResolveRouteMode(false, false));

            Assert.AreEqual(
                unchecked((int)0xC0040320),
                GridTerminalInteractionRules.CapturedBorealisGridTerminalInstance);
            Assert.AreEqual(152, GridTerminalInteractionRules.CapturedGridPlayfieldId);
            Assert.AreEqual(95350, GridTerminalInteractionRules.GridEnterTerminalTemplateId);
            Assert.AreEqual(95351, GridTerminalInteractionRules.GridExitTerminalTemplateId);
        }

        [TestMethod]
        public void SurgeryClinicRulesExposeCurrentCapturedConstantsAndTargetDecisions()
        {
            Assert.IsTrue(
                SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(
                    Terminal(SurgeryClinicInteractionRules.CapturedSurgeryClinicTerminalInstance),
                    0));
            Assert.IsTrue(
                SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(
                    Terminal(SurgeryClinicInteractionRules.CapturedAlternateSurgeryClinicTerminalInstance),
                    0));
            Assert.IsTrue(
                SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(
                    Terminal(SurgeryClinicInteractionRules.CapturedAreteLandingSurgeryClinicTerminalInstance),
                    0));
            Assert.IsTrue(
                SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(
                    Terminal(0x01020304),
                    SurgeryClinicInteractionRules.CapturedSurgeryClinicTemplateId));
            Assert.IsTrue(
                SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(
                    Terminal(0x01020304),
                    SurgeryClinicInteractionRules.CapturedImprovedSurgeryClinicTemplateId));
            Assert.IsFalse(
                SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(
                    new Identity { Type = IdentityType.Container, Instance = 0x01020304 },
                    SurgeryClinicInteractionRules.CapturedSurgeryClinicTemplateId));

            Assert.AreEqual(300, SurgeryClinicInteractionRules.SurgeryClinicCreditCost);
            Assert.AreEqual(0x26732, SurgeryClinicInteractionRules.SurgeryClinicNanoId);
            Assert.AreEqual(90000, SurgeryClinicInteractionRules.SurgeryClinicNanoDuration);
            Assert.AreEqual(300, SurgeryClinicInteractionRules.SurgeryClinicImplantAccessSeconds);
            Assert.AreEqual(124, SurgeryClinicInteractionRules.SurgeryClinicSpecialStatId);
            Assert.AreEqual(5, SurgeryClinicInteractionRules.SurgeryClinicSpecialLockSeconds);
            Assert.AreEqual(3500, SurgeryClinicInteractionRules.SurgeryClinicSpecialAvailableDelayMilliseconds);
        }

        [TestMethod]
        public void GenericInventoryAndFallbackRoutesKeepCurrentPrecedence()
        {
            AssertRoute(GenericCmdUseRoute.RexB18DBoxProgress, Terminal(0x01020306), rexB18DBoxProgressMatched: true);
            AssertRoute(GenericCmdUseRoute.InventoryItem, new Identity { Type = IdentityType.Inventory, Instance = 0x40 });
            AssertRoute(GenericCmdUseRoute.WearOrSocialBackpack, new Identity { Type = IdentityType.ArmorPage, Instance = 0x41 });
            AssertRoute(GenericCmdUseRoute.WearOrSocialBackpack, new Identity { Type = IdentityType.SocialPage, Instance = 0x42 });
            AssertRoute(GenericCmdUseRoute.BackpackContainer, new Identity { Type = IdentityType.Container, Instance = 0x43 });
            AssertRoute(GenericCmdUseRoute.PoolOnUseOrTrade, Terminal(0x01020307), poolContainsTarget: true);
            AssertRoute(GenericCmdUseRoute.StatelFallback, Terminal(0x01020308));
        }

        [TestMethod]
        public void RexB18DRulesExposeCurrentPreDispatchRouteDecision()
        {
            Assert.AreEqual(
                RexB18DInteractionRouteMode.RexB18DBoxProgress,
                RexB18DInteractionRules.ResolveRouteMode(true));
            Assert.AreEqual(
                RexB18DInteractionRouteMode.None,
                RexB18DInteractionRules.ResolveRouteMode(false));
        }

        [TestMethod]
        public void RexB18DRouteKeepsFirstPrecedenceBeforeLowerUseRoutes()
        {
            AssertRoute(
                GenericCmdUseRoute.RexB18DBoxProgress,
                new Identity { Type = IdentityType.Inventory, Instance = 0x40 },
                rexB18DBoxProgressMatched: true,
                poolContainsTarget: true);
        }

        [TestMethod]
        public void InventoryContainerRulesExposeCurrentRouteModeDecisions()
        {
            Assert.AreEqual(
                InventoryContainerInteractionRouteMode.InventoryItem,
                InventoryContainerInteractionRules.ResolveRouteMode(new Identity { Type = IdentityType.Inventory, Instance = 0x40 }));
            Assert.AreEqual(
                InventoryContainerInteractionRouteMode.WearOrSocialBackpack,
                InventoryContainerInteractionRules.ResolveRouteMode(new Identity { Type = IdentityType.ArmorPage, Instance = 0x41 }));
            Assert.AreEqual(
                InventoryContainerInteractionRouteMode.WearOrSocialBackpack,
                InventoryContainerInteractionRules.ResolveRouteMode(new Identity { Type = IdentityType.SocialPage, Instance = 0x42 }));
            Assert.AreEqual(
                InventoryContainerInteractionRouteMode.BackpackContainer,
                InventoryContainerInteractionRules.ResolveRouteMode(new Identity { Type = IdentityType.Container, Instance = 0x43 }));
            Assert.AreEqual(
                InventoryContainerInteractionRouteMode.None,
                InventoryContainerInteractionRules.ResolveRouteMode(Terminal(0x01020308)));
        }

        [TestMethod]
        public void StaticDynelRulesExposeCurrentRouteModeDecisions()
        {
            Assert.AreEqual(
                StaticDynelInteractionRouteMode.PoolOnUseOrTrade,
                StaticDynelInteractionRules.ResolveRouteMode(true));
            Assert.AreEqual(
                StaticDynelInteractionRouteMode.None,
                StaticDynelInteractionRules.ResolveRouteMode(false));
        }

        [TestMethod]
        public void StatelRulesExposeCurrentFallbackRouteModeDecision()
        {
            Assert.AreEqual(
                StatelInteractionRouteMode.StatelFallback,
                StatelInteractionRules.ResolveRouteMode(true));
            Assert.AreEqual(
                StatelInteractionRouteMode.None,
                StatelInteractionRules.ResolveRouteMode(false));
        }

        [TestMethod]
        public void StatelFallbackKeepsLowestPrecedenceAfterHigherPriorityRoutes()
        {
            AssertRoute(GenericCmdUseRoute.SurgeryClinic, Terminal(0x01020309), surgeryClinicTerminalMatched: true);
            AssertRoute(GenericCmdUseRoute.PoolOnUseOrTrade, Terminal(0x0102030A), poolContainsTarget: true);
            AssertRoute(GenericCmdUseRoute.StatelFallback, Terminal(0x0102030B));
        }

        [TestMethod]
        public void UseItemOnItemRulesExposeCurrentActionDecision()
        {
            Assert.AreEqual(
                UseItemOnItemInteractionRouteMode.UseItemOnItem,
                UseItemOnItemInteractionRules.ResolveRouteMode(GenericCmdAction.UseItemOnItem));
            Assert.AreEqual(
                UseItemOnItemInteractionRouteMode.None,
                UseItemOnItemInteractionRules.ResolveRouteMode(GenericCmdAction.Use));
        }













































        [TestMethod]
        public void SocialArmorPageSupportsBackAndShoulderMeshFunctions()
        {
            string socialArmorPage =
                ReadRepositoryFile(@"AORebirth\Libraries\Source\AORebirth.Core\Inventory\SocialArmorInventoryPage.cs");

            AssertContains(socialArmorPage, "(int)FunctionType.HeadMesh");
            AssertContains(socialArmorPage, "(int)FunctionType.BackMesh");
            AssertContains(socialArmorPage, "(int)FunctionType.Shouldermesh");
            AssertContains(socialArmorPage, "(int)FunctionType.Texture");
            AssertContains(socialArmorPage, "(int)FunctionType.ChangeBodyMesh");
        }





        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsCorpseLootInventoryTransfer()
        {
            string combatLootSmoke =
                ReadRepositoryFile(@"tools-temp\AOSharpLiveCapture\CombatLootSmoke.cs");
            AssertContains(combatLootSmoke, "private const int MoveToInventoryPlacement = 0x6F;");
            AssertContains(combatLootSmoke, "N3MessageType.InventoryUpdate");
            AssertContains(combatLootSmoke, "N3MessageType.ClientMoveItemToInventory");
            AssertContains(combatLootSmoke, "N3MessageType.ContainerAddItem");
            AssertContains(combatLootSmoke, "item.MoveToInventory(MoveToInventoryPlacement);");
            AssertContains(combatLootSmoke, "if (items.Count < this.itemCountBeforeMove)");
            AssertContains(combatLootSmoke, "OnlyUnlootableDuplicateUniqueItems(items)");
            AssertTextBefore(
                combatLootSmoke,
                "item.MoveToInventory(MoveToInventoryPlacement);",
                "this.Transition(SmokeState.WaitFirstMoved, \"first move sent\");");
        }











        private static void AssertRoute(
            GenericCmdUseRoute expected,
            Identity target,
            bool rexB18DBoxProgressMatched = false,
            bool isPrivateCityPlayfield = false,
            bool deadNpcCorpseRouted = false,
            bool capturedGridTerminalRouteMatched = false,
            bool gridEnterTerminalMatched = false,
            bool surgeryClinicTerminalMatched = false,
            bool poolContainsTarget = false)
        {
            var context = new GenericCmdUseRouteContext(target)
                          {
                              RexB18DBoxProgressMatched = rexB18DBoxProgressMatched,
                              IsPrivateCityPlayfield = isPrivateCityPlayfield,
                              DeadNpcCorpseRouted = deadNpcCorpseRouted,
                              CapturedGridTerminalRouteMatched = capturedGridTerminalRouteMatched,
                              GridEnterTerminalMatched = gridEnterTerminalMatched,
                              SurgeryClinicTerminalMatched = surgeryClinicTerminalMatched,
                              PoolContainsTarget = poolContainsTarget
                          };

            Assert.AreEqual(expected, GenericCmdUseRouteClassifier.Classify(context));
        }

        private static Identity Terminal(int instance)
        {
            return new Identity { Type = IdentityType.Terminal, Instance = instance };
        }

        private static Identity CityController(int instance)
        {
            return new Identity { Type = IdentityType.CityController, Instance = instance };
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        }

        private static string FindRepositoryRoot()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(currentDirectory, "AI_START_HERE.md")))
            {
                return currentDirectory;
            }

            string sourceFile = new StackTrace(true).GetFrame(0).GetFileName();
            string sourceRoot = FindRepositoryRootFromPath(sourceFile);
            if (!string.IsNullOrEmpty(sourceRoot))
            {
                return sourceRoot;
            }

            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "AI_START_HERE.md")))
                {
                    return current;
                }

                DirectoryInfo parent = Directory.GetParent(current);
                if (parent == null)
                {
                    break;
                }

                current = parent.FullName;
            }

            Assert.Fail("Could not find repository root for source guardrail.");
            return string.Empty;
        }

        private static string MakeRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', '\\');
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static string FindRepositoryRootFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            FileInfo fileInfo = new FileInfo(path);
            DirectoryInfo current = fileInfo.Directory;
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AI_START_HERE.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return null;
        }

        private static void AssertContains(string text, string expected)
        {
            Assert.IsTrue(text.Contains(expected), "Expected source to contain: " + expected);
        }

        private static void AssertTextBefore(string text, string first, string second)
        {
            int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = text.IndexOf(second, StringComparison.Ordinal);

            Assert.IsTrue(firstIndex >= 0, "Expected source to contain: " + first);
            Assert.IsTrue(secondIndex >= 0, "Expected source to contain: " + second);
            Assert.IsTrue(firstIndex < secondIndex, "Expected " + first + " before " + second + ".");
        }

        private static void AssertLastTextBefore(string text, string first, string second)
        {
            int firstIndex = text.LastIndexOf(first, StringComparison.Ordinal);
            int secondIndex = text.LastIndexOf(second, StringComparison.Ordinal);

            Assert.IsTrue(firstIndex >= 0, "Expected source to contain: " + first);
            Assert.IsTrue(secondIndex >= 0, "Expected source to contain: " + second);
            Assert.IsTrue(firstIndex < secondIndex, "Expected final " + first + " before " + second + ".");
        }

        private static void AssertDoesNotContain(string text, string unexpected)
        {
            Assert.IsFalse(text.Contains(unexpected), "Expected source not to contain: " + unexpected);
        }
    }
}
