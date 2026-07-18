namespace SmokeLounge.AOtomation.Messaging.Tests
{
    #region Usings ...

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Subway.Quests;

    #endregion

    [TestClass]
    public class WindcallerKarrecInteractionRulesTests
    {
        private static readonly Identity Karrec = new Identity
                                                  {
                                                      Type = IdentityType.CanbeAffected,
                                                      Instance = WindcallerKarrecInteractionRules.KarrecInstance
                                                  };

        [TestMethod]
        public void ExactPlayerPlayfieldNpcStateAndTwoOfferingsAreRequired()
        {
            Assert.AreEqual(
                KarrecTradeEligibility.Eligible,
                WindcallerKarrecInteractionRules.EvaluateTrade(
                    2034548837,
                    WindcallerKarrecInteractionRules.PlayfieldId,
                    Karrec,
                    true,
                    new[]
                    {
                        WindcallerKarrecInteractionRules.BurgerItemId,
                        WindcallerKarrecInteractionRules.CreditCardItemId
                    }));

            Assert.AreEqual(
                KarrecTradeEligibility.InvalidPlayer,
                WindcallerKarrecInteractionRules.EvaluateTrade(
                    0,
                    WindcallerKarrecInteractionRules.PlayfieldId,
                    Karrec,
                    true,
                    new[]
                    {
                        WindcallerKarrecInteractionRules.BurgerItemId,
                        WindcallerKarrecInteractionRules.CreditCardItemId
                    }));
            Assert.AreEqual(
                KarrecTradeEligibility.WrongPlayfield,
                WindcallerKarrecInteractionRules.EvaluateTrade(
                    2034548837,
                    127,
                    Karrec,
                    true,
                    new[]
                    {
                        WindcallerKarrecInteractionRules.BurgerItemId,
                        WindcallerKarrecInteractionRules.CreditCardItemId
                    }));
            Assert.AreEqual(
                KarrecTradeEligibility.MissionNotActive,
                WindcallerKarrecInteractionRules.EvaluateTrade(
                    2034548837,
                    WindcallerKarrecInteractionRules.PlayfieldId,
                    Karrec,
                    false,
                    new[]
                    {
                        WindcallerKarrecInteractionRules.BurgerItemId,
                        WindcallerKarrecInteractionRules.CreditCardItemId
                    }));
        }

        [TestMethod]
        public void WrongNpcAndWrongItemCombinationsFailClosed()
        {
            var wrongNpc = new Identity
                           {
                               Type = IdentityType.CanbeAffected,
                               Instance = WindcallerKarrecInteractionRules.KarrecInstance + 1
                           };
            Assert.AreEqual(
                KarrecTradeEligibility.WrongNpc,
                WindcallerKarrecInteractionRules.EvaluateTrade(
                    2034548837,
                    WindcallerKarrecInteractionRules.PlayfieldId,
                    wrongNpc,
                    true,
                    new[]
                    {
                        WindcallerKarrecInteractionRules.BurgerItemId,
                        WindcallerKarrecInteractionRules.CreditCardItemId
                    }));
            Assert.AreEqual(
                KarrecTradeEligibility.MissingOrWrongOfferings,
                WindcallerKarrecInteractionRules.EvaluateTrade(
                    2034548837,
                    WindcallerKarrecInteractionRules.PlayfieldId,
                    Karrec,
                    true,
                    new[]
                    {
                        WindcallerKarrecInteractionRules.BurgerItemId,
                        WindcallerKarrecInteractionRules.BurgerItemId
                    }));
            Assert.IsFalse(
                WindcallerKarrecInteractionRules.HasExactOfferings(
                    new[]
                    {
                        WindcallerKarrecInteractionRules.BurgerItemId,
                        WindcallerKarrecInteractionRules.CreditCardItemId,
                        123456
                    },
                    3,
                    true));
        }

        [TestMethod]
        public void GatewayRequiresExactTerminalIdentityTypeAndInstance()
        {
            Assert.IsTrue(
                WindcallerKarrecInteractionRules.IsGateway(
                    new Identity
                    {
                        Type = IdentityType.Terminal,
                        Instance = WindcallerKarrecInteractionRules.GatewayInstance
                    }));
            Assert.IsFalse(
                WindcallerKarrecInteractionRules.IsGateway(
                    new Identity
                    {
                        Type = IdentityType.VendingMachine,
                        Instance = WindcallerKarrecInteractionRules.GatewayInstance
                    }));
            Assert.IsFalse(
                WindcallerKarrecInteractionRules.IsGateway(
                    new Identity
                    {
                        Type = IdentityType.Terminal,
                        Instance = WindcallerKarrecInteractionRules.GatewayInstance + 1
                    }));
            Assert.IsFalse(
                WindcallerKarrecInteractionRules.AreCapturedPerkUpdateFieldsResolved(),
                "Unresolved captured PerkUpdate values must not be projected as player-independent research state.");
        }
    }
}
