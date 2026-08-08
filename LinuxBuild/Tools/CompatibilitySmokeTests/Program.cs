using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using Cell.Util.Collections;
using MsgPack;
using MsgPack.Serialization;
using SmokeLounge.AOtomation.Messaging.Serialization;

namespace AORebirth.LinuxBuild.CompatibilitySmokeTests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                VerifyAssembly(typeof(Packer).Assembly, "MsgPack", "0.4.0.0", "a2625990d5dc0167");
                VerifyAssembly(
                    typeof(MessageSerializer).Assembly,
                    "SmokeLounge.AOtomation.Messaging",
                    "0.62.1.0",
                    "366f6caa557bb5ed");
                VerifyAssembly(typeof(ImmutableList<int>).Assembly, "Cell.Util", "1.0.0.0", string.Empty);
                VerifyAssembly(typeof(locales.locales).Assembly, "locales", "1.0.0.0", string.Empty);
                VerifyMsgPackRuntime();
                VerifyTranslationResources();

                Console.WriteLine("PASS: Linux compatibility smoke tests");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception.Message);
                return 1;
            }
        }

        private static void VerifyAssembly(
            Assembly assembly,
            string expectedName,
            string expectedVersion,
            string expectedPublicKeyToken)
        {
            AssemblyName identity = assembly.GetName();
            Require(identity.Name == expectedName, "Unexpected assembly name: " + identity.Name);
            Require(identity.Version.ToString() == expectedVersion, "Unexpected version for " + expectedName);

            byte[] tokenBytes = identity.GetPublicKeyToken();
            string token = tokenBytes == null
                ? string.Empty
                : string.Concat(tokenBytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            Require(token == expectedPublicKeyToken, "Unexpected public key token for " + expectedName);
        }

        private static void VerifyMsgPackRuntime()
        {
            MessagePackSerializer<int[]> serializer = MessagePackSerializer.Create<int[]>();
            using (var stream = new MemoryStream())
            {
                serializer.Pack(stream, new[] { 1, 2 });
                byte[] expectedBytes = { 0x92, 0x01, 0x02 };
                Require(stream.ToArray().SequenceEqual(expectedBytes), "MsgPack byte vector changed");

                stream.Position = 0;
                int[] unpacked = serializer.Unpack(stream);
                Require(unpacked.SequenceEqual(new[] { 1, 2 }), "MsgPack round trip failed");
            }
        }

        private static void VerifyTranslationResources()
        {
            Require(
                locales.locales.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false) != null,
                "Invariant translation resource is missing");
            Require(
                locales.locales.ResourceManager.GetResourceSet(new CultureInfo("de"), true, false) != null,
                "German translation resource is missing");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
