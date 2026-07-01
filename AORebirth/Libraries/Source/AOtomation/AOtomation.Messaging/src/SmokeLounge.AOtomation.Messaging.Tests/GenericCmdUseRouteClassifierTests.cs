// This source code is licensed under the MIT license that can be found in the LICENSE file.

namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

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
    }
}
