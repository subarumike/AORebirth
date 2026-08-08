using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;

using Cell.Core;
using Cell.Util.Collections;
using MsgPack;
using MsgPack.Serialization;
using NLog;
using SmokeLounge.AOtomation.Messaging.Serialization;
using Utility;
using Utility.Config;

namespace AORebirth.LinuxBuild.CompatibilitySmokeTests
{
    internal static class Program
    {
        private static readonly int[] LegacyUtilityValues =
        {
            -32,
            -1,
            0,
            1,
            127,
            128,
            255,
            256,
            int.MinValue,
            int.MaxValue
        };

        private static int Main(string[] args)
        {
            try
            {
                string fixtureOutputDirectory = ParseFixtureOutputDirectory(args);
                VerifyAssembly(typeof(Packer).Assembly, "MsgPack", "0.4.0.0", "a2625990d5dc0167");
                VerifyAssembly(
                    typeof(MessageSerializer).Assembly,
                    "SmokeLounge.AOtomation.Messaging",
                    "0.62.1.0",
                    "366f6caa557bb5ed");
                VerifyAssembly(typeof(ImmutableList<int>).Assembly, "Cell.Util", "1.0.0.0", string.Empty);
                VerifyAssembly(typeof(locales.locales).Assembly, "locales", "1.0.0.0", string.Empty);
                VerifyAssembly(typeof(BufferManager).Assembly, "Cell.Core", "0.5.0.0", string.Empty);
                VerifyAssembly(typeof(Ionic.Zlib.ZlibStream).Assembly, "Ionic.Zlib", "1.9.1.5", string.Empty);
                VerifyAssembly(typeof(MessagePackZip).Assembly, "Utility", "1.0.0.0", string.Empty);
                VerifyMsgPackRuntime();
                VerifyTranslationResources();
                VerifyCellCoreResources();
                VerifyCellCoreBinaryReaders();
                VerifyCellCoreBuffers();
                VerifyCellCoreLoopback();
                VerifyLinuxCpuSnapshotParsing();
                VerifyUtilityRuntime();
                VerifyUtilityConfiguration();
                VerifyUtilityLogging();
                VerifyLegacyUtilityFixtures();
                if (fixtureOutputDirectory != null)
                {
                    WriteLinuxUtilityFixtures(fixtureOutputDirectory);
                }

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

        private static void VerifyCellCoreResources()
        {
            var resourceManager = new ResourceManager("Cell.Core.Localization.Cell.Core", typeof(BufferManager).Assembly);
            Require(
                resourceManager.GetString("BaseStart", CultureInfo.InvariantCulture) == "Starting the network layer!",
                "Cell.Core resources are missing or changed");
        }

        private static void VerifyCellCoreBinaryReaders()
        {
            Require(BitConverter.IsLittleEndian, "Cell.Core requires a little-endian deployment target");
            byte[] data =
            {
                0x34, 0x12, 0x00, 0x00,
                0x78, 0x56, 0x34, 0x12,
                0xFE, 0xFF, 0xFF, 0xFF,
                0x00, 0x00, 0xC0, 0x3F,
                0xEF, 0xCD, 0xAB, 0x89,
                0x67, 0x45, 0x23, 0x01
            };

            Require(data.GetUInt16(0) == 0x1234, "Cell.Core UInt16 field decoding changed");
            Require(data.GetUInt16AtByte(1) == 0x0012, "Cell.Core UInt16 byte decoding changed");
            Require(data.GetUInt32(1) == 0x12345678, "Cell.Core UInt32 decoding changed");
            Require(data.GetInt32(2) == -2, "Cell.Core Int32 decoding changed");
            Require(Math.Abs(data.GetFloat(3) - 1.5f) < 0.0001f, "Cell.Core float decoding changed");
            Require(data.GetUInt64(4) == 0x0123456789ABCDEFUL, "Cell.Core UInt64 decoding changed");
            Require(data.GetUInt32(6) == uint.MaxValue, "Cell.Core UInt32 bounds sentinel changed");
            Require(float.IsNaN(data.GetFloat(6)), "Cell.Core float bounds sentinel changed");

            var networkOrder = new byte[2];
            networkOrder.SetUShortBE(0, 0x1234);
            Require(networkOrder.SequenceEqual(new byte[] { 0x12, 0x34 }), "Cell.Core network byte order changed");
        }

        private static void VerifyCellCoreBuffers()
        {
            var manager = new BufferManager(2, 8);
            byte[] expected = Enumerable.Range(0, 20).Select(value => (byte)value).ToArray();
            using (SegmentStream stream = manager.CheckOutStream())
            {
                stream.Write(expected, 0, expected.Length);
                Require(stream.Length == expected.Length, "Cell.Core segment stream length changed");
                stream.Position = 0;
                var actual = new byte[expected.Length];
                int read = stream.Read(actual, 0, actual.Length);
                Require(read == actual.Length && actual.SequenceEqual(expected), "Cell.Core segment stream round trip failed");
            }

            Require(manager.UsedSegmentCount == 0, "Cell.Core buffer segment was not returned");
            manager.Dispose();
        }

        private static void VerifyCellCoreLoopback()
        {
            Require(NetworkUtil.GetMatchingLocalIP(IPAddress.Loopback).Equals(IPAddress.Loopback),
                "Cell.Core loopback address selection changed");

            using (var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                SocketHelpers.SetListenSocketOptions(listener);
                listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                listener.Listen(1);
                client.Connect((IPEndPoint)listener.LocalEndPoint);
                using (Socket server = listener.Accept())
                {
                    byte[] request = { 0x41, 0x4F };
                    client.Send(request);
                    var received = new byte[request.Length];
                    Require(server.Receive(received) == request.Length && received.SequenceEqual(request),
                        "Cell.Core loopback receive failed");
                    server.Send(new byte[] { 0x4F, 0x4B });
                    var response = new byte[2];
                    Require(client.Receive(response) == response.Length
                            && response.SequenceEqual(new byte[] { 0x4F, 0x4B }),
                        "Cell.Core loopback response failed");
                }
            }
        }

        private static void VerifyUtilityRuntime()
        {
            float cpuLoad = CpuRamUtilization.GetCpuLoad();
            float availableRam = CpuRamUtilization.GetRamLoad();
            Require(cpuLoad >= 0.0f && cpuLoad <= 100.0f, "Portable CPU metric is outside its valid range");
            Require(availableRam >= 0.0f, "Portable RAM metric is outside its valid range");

            string listPath = Path.Combine(Path.GetTempPath(), "aorebirth-linux-list-" + Guid.NewGuid() + ".dat");
            string dictionaryPath = Path.Combine(
                Path.GetTempPath(),
                "aorebirth-linux-dictionary-" + Guid.NewGuid() + ".dat");
            try
            {
                var expectedList = new List<int> { -1, 0, 1, 127, 128, 300 };
                MessagePackZip.CompressData(listPath, "stage1", expectedList, 2);
                List<int> actualList = MessagePackZip.UncompressData<int>(listPath);
                Require(actualList.SequenceEqual(expectedList), "Utility list compression round trip failed");

                var expectedDictionary = new Dictionary<string, int>
                {
                    { "alpha", 1 },
                    { "beta", 2 }
                };
                MessagePackZip.CompressData(dictionaryPath, "stage1", expectedDictionary, 2);
                Dictionary<string, int> actualDictionary = MessagePackZip.UncompressData<string, int>(dictionaryPath);
                Require(
                    actualDictionary.Count == expectedDictionary.Count
                    && expectedDictionary.All(pair => actualDictionary[pair.Key] == pair.Value),
                    "Utility dictionary compression round trip failed");
            }
            finally
            {
                if (File.Exists(listPath))
                {
                    File.Delete(listPath);
                }

                if (File.Exists(dictionaryPath))
                {
                    File.Delete(dictionaryPath);
                }
            }
        }

        private static void VerifyLinuxCpuSnapshotParsing()
        {
            MethodInfo parser = typeof(CpuRamUtilization).GetMethod(
                "TryParseLinuxCpuSnapshot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(parser != null, "Portable CPU snapshot parser is missing");

            object[] parameters = { "cpu  100 20 30 400 5 6 7 8 50 10", 0UL, 0UL };
            Require((bool)parser.Invoke(null, parameters), "Portable CPU snapshot parser rejected valid data");
            Require((ulong)parameters[1] == 576UL, "Portable CPU snapshot double-counts guest time");
            Require((ulong)parameters[2] == 405UL, "Portable CPU idle calculation changed");
        }

        private static void VerifyUtilityConfiguration()
        {
            string originalDirectory = Environment.CurrentDirectory;
            string originalConnection = Environment.GetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION");
            const string ExpectedConnection = "Server=stage1-linux-parity;Database=aorebirth;";
            string temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "aorebirth-linux-config-" + Guid.NewGuid());
            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                Environment.CurrentDirectory = temporaryDirectory;
                var configSerializer = new System.Xml.Serialization.XmlSerializer(typeof(Config));
                using (FileStream seedStream = File.Create(Path.Combine(temporaryDirectory, "Config.xml")))
                {
                    configSerializer.Serialize(seedStream, new Config());
                }

                Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", ExpectedConnection);
                Config configuration = ConfigReadWrite.Instance.CurrentConfig;
                Require(configuration != null, "Utility configuration fallback failed");
                Require(configuration.MysqlConnection == ExpectedConnection, "Utility MySQL environment override failed");
                File.Delete(Path.Combine(temporaryDirectory, "Config.xml"));
                Require(ConfigReadWrite.Instance.SaveConfig(), "Utility configuration save failed");

                string[] configFiles = Directory.GetFiles(temporaryDirectory, "*")
                    .Where(path => string.Equals(
                        Path.GetFileName(path),
                        "Config.xml",
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                Require(
                    configFiles.Length == 1
                    && string.Equals(Path.GetFileName(configFiles[0]), "Config.xml", StringComparison.Ordinal),
                    "Utility configuration filename casing changed");
                Require(!File.ReadAllBytes(configFiles[0]).Contains((byte)0), "Utility configuration contains trailing nulls");

                using (FileStream stream = File.OpenRead(configFiles[0]))
                {
                    Require(configSerializer.Deserialize(stream) is Config, "Utility configuration cannot be reloaded");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AO_REBIRTH_MYSQL_CONNECTION", originalConnection);
                Environment.CurrentDirectory = originalDirectory;
                Directory.Delete(temporaryDirectory, true);
            }
        }

        private static void VerifyUtilityLogging()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "aorebirth-linux-log-" + Guid.NewGuid() + ".log");
            try
            {
                LogUtil.SetupFileLogging(logPath, LogLevel.Info);
                LogManager.GetLogger("LinuxCompatibilitySmoke").Info("stage1 logging smoke");
                LogManager.Flush(TimeSpan.FromSeconds(5));
                LogManager.Shutdown();
                Require(
                    File.Exists(logPath) && File.ReadAllText(logPath).Contains("stage1 logging smoke"),
                    "Utility NLog file target failed");
            }
            finally
            {
                LogManager.Shutdown();
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                }
            }
        }

        private static void VerifyLegacyUtilityFixtures()
        {
            string fixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            string legacyListPath = Path.Combine(fixturesDirectory, "LegacyUtilityList.dat");
            string legacyDictionaryPath = Path.Combine(fixturesDirectory, "LegacyUtilityDictionary.dat");
            VerifyLegacyUtilityFixtureManifest(fixturesDirectory);

            List<int> legacyList = MessagePackZip.UncompressData<int>(legacyListPath);
            Require(legacyList.SequenceEqual(LegacyUtilityValues), "Linux could not read the legacy list fixture");

            Dictionary<string, int> expectedDictionary = CreateLegacyUtilityDictionary();
            Dictionary<string, int> legacyDictionary =
                MessagePackZip.UncompressData<string, int>(legacyDictionaryPath);
            Require(
                legacyDictionary.Count == expectedDictionary.Count
                && expectedDictionary.All(pair => legacyDictionary[pair.Key] == pair.Value),
                "Linux could not read the legacy dictionary fixture");
        }

        private static void VerifyLegacyUtilityFixtureManifest(string fixturesDirectory)
        {
            string manifestPath = Path.Combine(fixturesDirectory, "LegacyUtilityFixtures.manifest");
            string[] lines = File.ReadAllLines(manifestPath);
            Require(lines.Length == 2, "Legacy Utility fixture manifest changed");
            var expectedNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "LegacyUtilityList.dat",
                "LegacyUtilityDictionary.dat"
            };

            foreach (string line in lines)
            {
                string[] fields = line.Split('|');
                Require(fields.Length == 3, "Legacy Utility fixture manifest is malformed");
                Require(expectedNames.Remove(fields[0]), "Unexpected legacy Utility fixture: " + fields[0]);
                string fixturePath = Path.Combine(fixturesDirectory, fields[0]);
                Require(File.Exists(fixturePath), "Legacy Utility fixture is missing: " + fields[0]);
                Require(
                    new FileInfo(fixturePath).Length == long.Parse(fields[1], CultureInfo.InvariantCulture),
                    "Legacy Utility fixture length changed: " + fields[0]);
                Require(
                    string.Equals(ComputeSha256(fixturePath), fields[2], StringComparison.Ordinal),
                    "Legacy Utility fixture hash changed: " + fields[0]);
            }

            Require(expectedNames.Count == 0, "Legacy Utility fixture manifest is incomplete");
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return string.Concat(
                    sha256.ComputeHash(stream)
                        .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static string ParseFixtureOutputDirectory(string[] args)
        {
            if (args.Length == 0)
            {
                return null;
            }

            if (args.Length != 2 || args[0] != "--write-utility-fixtures")
            {
                throw new ArgumentException(
                    "Usage: CompatibilitySmokeTests [--write-utility-fixtures <directory>]");
            }

            return Path.GetFullPath(args[1]);
        }

        private static void WriteLinuxUtilityFixtures(string directory)
        {
            Directory.CreateDirectory(directory);
            MessagePackZip.CompressData(
                Path.Combine(directory, "LegacyUtilityList.dat"),
                "stage1-legacy",
                LegacyUtilityValues.ToList(),
                3);
            MessagePackZip.CompressData(
                Path.Combine(directory, "LegacyUtilityDictionary.dat"),
                "stage1-legacy",
                CreateLegacyUtilityDictionary(),
                3);
        }

        private static Dictionary<string, int> CreateLegacyUtilityDictionary()
        {
            var result = new Dictionary<string, int>();
            for (int index = 0; index < LegacyUtilityValues.Length; index++)
            {
                result.Add("value-" + index, LegacyUtilityValues[index]);
            }

            return result;
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
