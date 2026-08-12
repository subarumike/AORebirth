namespace SmokeLounge.AOtomation.Messaging.Tests
{
    using System;
    using System.Net;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Utility.Network;

    [TestClass]
    public class EngineBindPolicyTests
    {
        [TestMethod]
        public void MissingConfigurationSelectsLoopback()
        {
            EngineBindPolicy policy = EngineBindPolicy.Resolve(null);

            Assert.AreEqual(EngineBindMode.Loopback, policy.Mode);
            Assert.AreEqual(IPAddress.Loopback, policy.Address);
            Assert.AreEqual("127.0.0.1", policy.AddressText);
        }

        [TestMethod]
        public void ExplicitLoopbackSelectsLoopbackAddress()
        {
            EngineBindPolicy policy = EngineBindPolicy.Resolve("Loopback");

            Assert.AreEqual(EngineBindMode.Loopback, policy.Mode);
            Assert.AreEqual(IPAddress.Loopback, policy.Address);
            Assert.AreEqual("127.0.0.1", policy.AddressText);
        }

        [TestMethod]
        public void ExplicitPublicSelectsWildcardAddress()
        {
            EngineBindPolicy policy = EngineBindPolicy.Resolve("Public");

            Assert.AreEqual(EngineBindMode.Public, policy.Mode);
            Assert.AreEqual(IPAddress.Any, policy.Address);
            Assert.AreEqual("0.0.0.0", policy.AddressText);
        }

        [TestMethod]
        public void InvalidConfigurationFailsClosed()
        {
            AssertInvalid("Internet");
        }

        [TestMethod]
        public void EmptyOrWhitespaceConfigurationFailsClosed()
        {
            AssertInvalid(string.Empty);
            AssertInvalid("   ");
        }

        private static void AssertInvalid(string value)
        {
            try
            {
                EngineBindPolicy.Resolve(value);
                Assert.Fail("Expected invalid bind mode to fail.");
            }
            catch (InvalidOperationException exception)
            {
                StringAssert.Contains(exception.Message, EngineBindPolicy.EnvironmentVariableName);
            }
        }
    }
}
