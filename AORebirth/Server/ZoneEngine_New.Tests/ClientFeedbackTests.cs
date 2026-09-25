namespace ZoneEngine_New.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using ZoneEngine_New.Core.Helpers;

    [TestClass]
    public sealed class ClientFeedbackTests
    {
        [TestMethod]
        public void ElfHashMatchesCategory110TextIds()
        {
            Assert.AreEqual(142769077u, ClientFeedback.ElfHash("Feedback_MustBeOutside"));
            Assert.AreEqual(116606052u, ClientFeedback.ElfHash("Feedback_StartingAttackFailed"));
            Assert.AreEqual(179318652u, ClientFeedback.ElfHash("Feedback_PvpNotAllowedSinceYouAreNeutral"));
            Assert.AreEqual(238526887u, ClientFeedback.ElfHash("Feedback_NanobotsAreRecharging"));
            Assert.AreEqual(90418825u, ClientFeedback.ElfHash("Feedback_NanoprogramDidNotActivateNotEnoughNanoenergy"));
        }
    }
}
