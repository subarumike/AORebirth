// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using System;
    using System.Diagnostics;
    using System.IO;

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
        public void InventoryContainerRuntimeServiceOwnsGenericCmdInventoryOrchestration()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string inventoryHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\InventoryContainerInteractionHandler.cs");
            string useItemOnItemHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\UseItemOnItemInteractionHandler.cs");
            string genericCmdHandler =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\MessageHandlers\GenericCmdMessageHandler.cs");

            AssertContains(service, "public sealed class InventoryContainerRuntimeService");
            AssertContains(service, "TryHandleGenericCmdUse");
            AssertContains(service, "TryHandleUseItemOnItem");
            AssertContains(service, "client.Controller.UseItem(target);");
            AssertContains(service, "this.TryUseBackpackContainer(client.Controller.Character, target)");
            AssertContains(service, "BackpackContainerActionMessageHandler.Default.SendClose");
            AssertContains(service, "client.Controller.UseStatel(message.Target[1], EventType.OnUseItemOn);");

            AssertContains(
                inventoryHandler,
                "return InventoryContainerRuntimeService.Default.TryHandleGenericCmdUse(client, message, target);");
            AssertDoesNotContain(inventoryHandler, "client.Controller.UseItem(target);");
            AssertDoesNotContain(inventoryHandler, "client.Controller.TryUseBackpackContainer(target)");
            AssertDoesNotContain(inventoryHandler, "BackpackContainerActionMessageHandler.Default.SendClose");

            AssertContains(
                useItemOnItemHandler,
                "return InventoryContainerRuntimeService.Default.TryHandleUseItemOnItem(client, message);");
            AssertDoesNotContain(useItemOnItemHandler, "Pool.Instance.GetObject<IInventoryPage>");
            AssertDoesNotContain(useItemOnItemHandler, "client.Controller.UseStatel");

            AssertContains(genericCmdHandler, "InventoryContainerInteractionHandler.Default.TryHandleUse");
            AssertContains(genericCmdHandler, "UseItemOnItemInteractionHandler.Default.TryHandle");

            foreach (string forbiddenReference in new[]
                                                {
                                                    "NpcCombat",
                                                    "NpcCorpseLifecycle",
                                                    "NpcPatrol",
                                                    "PrivateCityReadyInit",
                                                    "OrgClient",
                                                    "CityController",
                                                    "GuestKey",
                                                    "AOSharpLiveCapture",
                                                    "CheckDatabase"
                                                })
            {
                AssertDoesNotContain(service, forbiddenReference);
            }
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsBankOpenAndSlotSelection()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string bankHandler =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\MessageHandlers\BankMessageHandler.cs");
            string openBankFunction =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\Functions\GameFunctions\openbank.cs");

            AssertContains(service, "public void OpenBank(ICharacter character)");
            AssertContains(service, "BankMessageHandler.Default.Send(character);");
            AssertContains(service, "public BankSlot[] ResolveBankSlots(ICharacter character)");
            AssertContains(service, "character.BaseInventory.Pages[(int)IdentityType.Bank].ToInventoryArray();");

            AssertContains(
                bankHandler,
                "x.BankSlots = InventoryContainerRuntimeService.Default.ResolveBankSlots(character);");
            AssertDoesNotContain(bankHandler, "Pages[(int)IdentityType.Bank]");

            AssertContains(
                openBankFunction,
                "InventoryContainerRuntimeService.Default.OpenBank((ICharacter)self);");
            AssertDoesNotContain(openBankFunction, "BankMessageHandler.Default.Send");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsBackpackMoveToInventoryLifecycle()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string clientMoveHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ClientMoveItemToInventoryMessageHandler.cs");

            AssertContains(service, "public bool TryMoveBackpackItemToInventory");
            AssertContains(service, "message.SourceContainer.Type != IdentityType.Backpack");
            AssertContains(service, "character.BaseInventory.TryGetBackpackPageByHandle");
            AssertContains(service, "receivingPage.Add(toPlacement, itemFrom)");
            AssertContains(service, "backpackPage.Remove(fromPlacement)");
            AssertContains(
                service,
                "this.SendMoveItemToInventoryAck(character, message.SourceContainer, message.TargetPlacement);");
            AssertContains(service, "this.PersistClientMoveItemToInventory(character, \"backpack move\");");

            AssertContains(
                clientMoveHandler,
                "InventoryContainerRuntimeService.Default.TryMoveBackpackItemToInventory(character, message)");
            AssertDoesNotContain(clientMoveHandler, "private bool TryMoveBackpackItemToInventory");
            AssertDoesNotContain(clientMoveHandler, "TryGetBackpackPageByHandle");
            AssertDoesNotContain(clientMoveHandler, "DecodeBackpackHandle");
            AssertDoesNotContain(clientMoveHandler, "TryRemoveInventoryRollback");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsMoveAckAndPersistenceSurfaces()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string clientMoveHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ClientMoveItemToInventoryMessageHandler.cs");

            AssertContains(service, "public void SendMoveItemToInventoryAck");
            AssertContains(service, "new ContainerAddItemMessage");
            AssertContains(service, "public void PersistClientMoveItemToInventory");
            AssertContains(service, "character.BaseInventory.Write();");

            AssertContains(service, "this.SendMoveItemToInventoryAck(");
            AssertContains(service, "this.PersistClientMoveItemToInventory(");
            AssertDoesNotContain(clientMoveHandler, "InventoryContainerRuntimeService.Default.SendMoveItemToInventoryAck");
            AssertDoesNotContain(clientMoveHandler, "InventoryContainerRuntimeService.Default.PersistClientMoveItemToInventory");
            AssertDoesNotContain(clientMoveHandler, "private void SendMoveAck");
            AssertDoesNotContain(clientMoveHandler, "private void PersistCharacterInventory");
            AssertDoesNotContain(clientMoveHandler, "new ContainerAddItemMessage");
            AssertDoesNotContain(clientMoveHandler, "character.BaseInventory.Write();");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsMovePageLookupSurfaces()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string clientMoveHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ClientMoveItemToInventoryMessageHandler.cs");

            AssertContains(service, "public bool TryResolveMoveSourcePage");
            AssertContains(service, "character.BaseInventory.Pages.ContainsKey((int)sourceContainer.Type)");
            AssertContains(service, "character.BaseInventory.PageFromSlot(sourceContainer.Instance)");
            AssertContains(service, "public IInventoryPage ResolveMoveTargetPage");
            AssertContains(service, "character.BaseInventory.PageFromSlot(targetPlacement)");

            AssertContains(service, "this.TryResolveMoveSourcePage(");
            AssertContains(service, "this.ResolveMoveTargetPage(");
            AssertDoesNotContain(clientMoveHandler, "InventoryContainerRuntimeService.Default.TryResolveMoveSourcePage");
            AssertDoesNotContain(clientMoveHandler, "InventoryContainerRuntimeService.Default.ResolveMoveTargetPage");
            AssertDoesNotContain(clientMoveHandler, "private bool TryGetSourcePage");
            AssertDoesNotContain(clientMoveHandler, "private IInventoryPage GetTargetPage");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsOwnedInventoryMoveBranch()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string clientMoveHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ClientMoveItemToInventoryMessageHandler.cs");

            AssertContains(service, "public bool TryMoveOwnedInventoryItem");
            AssertContains(service, "WeaponItemFullUpdate.SendWeaponDefinition(character, itemFrom);");
            AssertContains(service, "equipTo.HotSwap(sendingPage, fromPlacement, toPlacement);");
            AssertContains(service, "equipTo.Equip(sendingPage, fromPlacement, toPlacement);");
            AssertContains(service, "unequipFrom.Unequip(fromPlacement, receivingPage, toPlacement);");
            AssertContains(service, "sendingPage.Remove(fromPlacement);");
            AssertContains(service, "receivingPage.Add(toPlacement, itemFrom);");

            AssertContains(
                clientMoveHandler,
                "InventoryContainerRuntimeService.Default.TryMoveOwnedInventoryItem(character, message, client)");
            AssertDoesNotContain(clientMoveHandler, "private bool TryMoveOwnedInventoryItem");
            AssertDoesNotContain(clientMoveHandler, "private bool CanEquipToPage");
            AssertDoesNotContain(clientMoveHandler, "private bool RequiresImplantAccess");
            AssertDoesNotContain(clientMoveHandler, "private void SendImplantAccessDenied");
            AssertDoesNotContain(clientMoveHandler, "private void WaitForEquipVisualSync");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsCharacterStateInventoryPageBoundary()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string fullCharacterHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\FullCharacterMessageHandler.cs");
            string weaponItemFullUpdate =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\Packets\WeaponItemFullUpdate.cs");

            AssertContains(service, "public IEnumerable<IInventoryPage> CharacterStateInventoryPages");
            AssertContains(service, "foreach (IInventoryPage page in character.BaseInventory.Pages.Values)");
            AssertContains(service, "page is BankInventoryPage");

            AssertContains(
                fullCharacterHandler,
                "InventoryContainerRuntimeService.Default.CharacterStateInventoryPages(character)");
            AssertContains(
                weaponItemFullUpdate,
                "InventoryContainerRuntimeService.Default.CharacterStateInventoryPages(character)");
            AssertDoesNotContain(fullCharacterHandler, "ivp is BankInventoryPage");
            AssertDoesNotContain(weaponItemFullUpdate, "page is BankInventoryPage");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsContainerAddItemTargetPageResolution()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string containerAddItemHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ContainerAddItemMessageHandler.cs");

            AssertContains(service, "public Identity ResolveContainerAddItemTargetIdentity");
            AssertContains(service, "toIdentity.Type == IdentityType.IncomingTradeWindow");
            AssertContains(service, "toIdentity.Type = IdentityType.CanbeAffected;");
            AssertContains(service, "public IInventoryPage ResolveContainerAddItemReceivingPage");
            AssertContains(service, "target.Type == IdentityType.IncomingTradeWindow");
            AssertContains(service, "itemReceiver.BaseInventory.Pages[(int)IdentityType.Bank]");
            AssertContains(service, "public int ResolveContainerAddItemTargetPlacement");

            AssertContains(
                containerAddItemHandler,
                "InventoryContainerRuntimeService.Default.ResolveContainerAddItemTargetIdentity");
            AssertContains(
                containerAddItemHandler,
                "InventoryContainerRuntimeService.Default.ResolveContainerAddItemReceivingPage");
            AssertContains(
                containerAddItemHandler,
                "InventoryContainerRuntimeService.Default.ResolveContainerAddItemTargetPlacement");
            AssertDoesNotContain(containerAddItemHandler, "toIdentity.Type = IdentityType.CanbeAffected;");
            AssertDoesNotContain(containerAddItemHandler, "itemReceiver.BaseInventory.Pages[(int)IdentityType.Bank]");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsInventoryToBackpackMove()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string clientContainerAddItemHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ClientContainerAddItemMessageHandler.cs");

            AssertContains(service, "public bool TryMoveInventoryItemToBackpack");
            AssertContains(service, "message.Target.Type != IdentityType.Container");
            AssertContains(service, "character.BaseInventory.TryGetBackpackPage(message.Target, out backpackPage)");
            AssertContains(service, "InventoryItemRules.IsBackpackContainerItem(item)");
            AssertContains(service, "new ContainerAddItemMessage");
            AssertContains(service, "Persisted inventory after ClientContainerAddItem backpack move");
            AssertContains(service, "private void TryRemoveBackpackRollback");

            AssertContains(
                clientContainerAddItemHandler,
                "InventoryContainerRuntimeService.Default.TryMoveInventoryItemToBackpack(character, message)");
            AssertDoesNotContain(clientContainerAddItemHandler, "private bool TryMoveInventoryItemToBackpack");
            AssertDoesNotContain(clientContainerAddItemHandler, "TryRemoveBackpackRollback");
            AssertDoesNotContain(clientContainerAddItemHandler, "InventoryItemRules.IsBackpackContainerItem");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsInventoryToBankDeposit()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string clientContainerAddItemHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ClientContainerAddItemMessageHandler.cs");

            AssertContains(service, "public bool TryDepositInventoryItemToBank");
            AssertContains(service, "private static bool IsInventoryToBankDeposit");
            AssertContains(service, "message.Target.Type == IdentityType.IncomingTradeWindow");
            AssertContains(service, "message.Target.Instance != character.Identity.Instance");
            AssertContains(service, "character.BaseInventory.Pages.TryGetValue((int)IdentityType.Bank, out bankPage)");
            AssertContains(service, "private void TryRemoveBankRollback");
            AssertContains(service, "Persisted inventory after ClientContainerAddItem bank deposit");

            AssertContains(
                clientContainerAddItemHandler,
                "InventoryContainerRuntimeService.Default.TryDepositInventoryItemToBank(character, message)");
            AssertDoesNotContain(clientContainerAddItemHandler, "private bool IsInventoryToBankDeposit");
            AssertDoesNotContain(clientContainerAddItemHandler, "private void TryRemoveBankRollback");
            AssertDoesNotContain(clientContainerAddItemHandler, "character.BaseInventory.Pages.TryGetValue((int)IdentityType.Bank");
            AssertDoesNotContain(clientContainerAddItemHandler, "Persisted inventory after ClientContainerAddItem bank deposit");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsNonEquipmentContainerTransfer()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string containerAddItemHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ContainerAddItemMessageHandler.cs");

            AssertContains(service, "public void MoveNonEquipmentContainerItem");
            AssertContains(service, "message.TargetPlacement = receivingPage.FindFreeSlot();");
            AssertContains(service, "IItem item = sendingPage.Remove(fromPlacement);");
            AssertContains(service, "receivingPage.Add(message.TargetPlacement, item);");
            AssertContains(service, "character.Send(message);");

            AssertContains(
                containerAddItemHandler,
                "InventoryContainerRuntimeService.Default.MoveNonEquipmentContainerItem(");
            AssertDoesNotContain(containerAddItemHandler, "message.TargetPlacement = receivingPage.FindFreeSlot();");
            AssertDoesNotContain(containerAddItemHandler, "IItem item = sendingPage.Remove(fromPlacement);");
            AssertDoesNotContain(containerAddItemHandler, "receivingPage.Add(message.TargetPlacement, item);");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsBackpackOpenCloseLifecycle()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string playerController =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\Controllers\PlayerController.cs");

            AssertContains(service, "public bool TryUseBackpackContainer");
            AssertContains(service, "public bool TryOpenBackpackContainer");
            AssertContains(service, "BackpackContainerActionMessageHandler.Default.SendClose(character, containerIdentity)");
            AssertContains(service, "BackpackContainerActionMessageHandler.Default.SendOpen(character, containerIdentity)");
            AssertContains(service, "InventoryUpdateMessageHandler.Default.SendContainerIntroduce");
            AssertContains(service, "InventoryUpdateMessageHandler.Default.SendFreshContainerOpen");
            AssertContains(service, "private static bool IsBackpackUseSlot");
            AssertContains(service, "private static bool TryResolveBackpackContainerIdentity");
            AssertContains(service, "private static bool IsItemUsable");
            AssertContains(service, "this.TryUseBackpackContainer(client.Controller.Character, target)");

            AssertContains(
                playerController,
                "InventoryContainerRuntimeService.Default.TryOpenBackpackContainer(this.Character, itemPosition, item)");
            AssertContains(
                playerController,
                "InventoryContainerRuntimeService.Default.TryUseBackpackContainer(this.Character, itemPosition)");
            AssertDoesNotContain(playerController, "private bool TryOpenBackpackContainer");
            AssertDoesNotContain(playerController, "private bool IsBackpackUseSlot");
            AssertDoesNotContain(playerController, "private bool TryResolveBackpackContainerIdentity");
            AssertDoesNotContain(playerController, "private bool IsItemUsable");
        }

        [TestMethod]
        public void InventoryContainerRuntimeServiceOwnsContainerEquipAccessAndRequirementChecks()
        {
            string service =
                ReadRepositoryFile(@"AORebirth\Server\ZoneEngine\Core\InventoryContainerRuntimeService.cs");
            string containerAddItemHandler =
                ReadRepositoryFile(
                    @"AORebirth\Server\ZoneEngine\Core\MessageHandlers\ContainerAddItemMessageHandler.cs");

            AssertContains(service, "public bool TryRejectInventoryPageAccess");
            AssertContains(service, "public bool CanMoveContainerItemToPage");
            AssertContains(service, "private static AOAction ResolveContainerAddItemAction");
            AssertContains(service, "item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWear)");
            AssertContains(service, "item.ItemActions.SingleOrDefault(x => x.ActionType == ActionType.ToWield)");
            AssertContains(service, "No suitable action found for equipping to this page");

            AssertContains(
                containerAddItemHandler,
                "InventoryContainerRuntimeService.Default.TryRejectInventoryPageAccess(");
            AssertContains(
                containerAddItemHandler,
                "InventoryContainerRuntimeService.Default.CanMoveContainerItemToPage(");
            AssertDoesNotContain(containerAddItemHandler, "private AOAction getAction");
            AssertDoesNotContain(containerAddItemHandler, "private bool RequiresImplantAccess");
            AssertDoesNotContain(containerAddItemHandler, "private bool HasImplantAccess");
            AssertDoesNotContain(containerAddItemHandler, "private void SendImplantAccessDenied");
            AssertDoesNotContain(containerAddItemHandler, "item.ItemActions.SingleOrDefault");
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

        private static void AssertDoesNotContain(string text, string unexpected)
        {
            Assert.IsFalse(text.Contains(unexpected), "Expected source not to contain: " + unexpected);
        }
    }
}
