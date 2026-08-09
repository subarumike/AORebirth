using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;

using AORebirth.Core.Entities;
using Ionic.Zlib;

namespace AORebirth.LinuxBuild.Stage8OfflineSmokeTests
{
    internal static class Program
    {
        private const string RepositoryRootArgument = "--repository-root";
        private const string ZoneOutputArgument = "--zone-output";

        public static int Main(string[] args)
        {
            try
            {
                string repositoryRoot = ReadArgument(args, RepositoryRootArgument);
                string zoneOutput = ReadArgument(args, ZoneOutputArgument);

                VerifyAssembly(typeof(Character).Assembly, "AORebirth.Core", "1.0.0.0");
                VerifyAssembly(
                    typeof(ZoneEngine.Core.Playfields.PlayfieldLoader).Assembly,
                    "PlayfieldLoader",
                    "1.0.0.0");
                VerifyAssembly(Assembly.Load(new AssemblyName("ZoneEngine")), "ZoneEngine", "1.0.0.0");
                VerifyZoneReferences();
                VerifyJavaScriptSerializerCompatibility();
                VerifyZlibDiagnosticsCompatibility();
                VerifyZoneCopiedAssets(repositoryRoot, zoneOutput);

                Console.WriteLine("PASS: Stage 8 offline ZoneEngine smoke");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception.Message);
                return 1;
            }
        }

        private static string ReadArgument(string[] args, string name)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return Path.GetFullPath(args[index + 1]);
                }
            }

            throw new ArgumentException("Missing required argument: " + name);
        }

        private static void VerifyAssembly(Assembly assembly, string expectedName, string expectedVersion)
        {
            AssemblyName identity = assembly.GetName();
            Require(identity.Name == expectedName, "Unexpected assembly name: " + identity.Name);
            Require(
                identity.Version.ToString() == expectedVersion,
                "Unexpected version for " + expectedName + ": " + identity.Version);
        }

        private static void VerifyZoneReferences()
        {
            Assembly zoneAssembly = Assembly.Load(new AssemblyName("ZoneEngine"));
            string[] references = zoneAssembly.GetReferencedAssemblies()
                .Select(name => name.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Require(!references.Contains("NBug", StringComparer.Ordinal), "ZoneEngine Linux must not reference NBug");
            Require(
                !references.Contains("System.Web.Extensions", StringComparer.Ordinal),
                "ZoneEngine Linux must not reference System.Web.Extensions");
        }

        private static void VerifyJavaScriptSerializerCompatibility()
        {
            Assembly zoneAssembly = Assembly.Load(new AssemblyName("ZoneEngine"));
            Type serializerType = zoneAssembly.GetType(
                "System.Web.Script.Serialization.JavaScriptSerializer",
                true);
            object serializer = Activator.CreateInstance(serializerType, true);
            serializerType.GetProperty("MaxJsonLength").SetValue(serializer, int.MaxValue, null);

            MethodInfo deserialize = serializerType.GetMethod("Deserialize").MakeGenericMethod(typeof(CaseDto));
            var dto = (CaseDto)deserialize.Invoke(serializer, new object[] { "{\"value\":7,\"Name\":\"Arete\"}" });
            Require(dto.Value == 7, "JavaScriptSerializer compatibility lost case-insensitive values");
            Require(dto.Name == "Arete", "JavaScriptSerializer compatibility lost string values");

            MethodInfo serialize = serializerType.GetMethod("Serialize");
            string json = (string)serialize.Invoke(serializer, new object[] { dto });
            Require(json.Contains("Value", StringComparison.Ordinal), "JavaScriptSerializer compatibility lost fields");
            Require(json.Contains("Name", StringComparison.Ordinal), "JavaScriptSerializer compatibility lost properties");
        }

        private static void VerifyZlibDiagnosticsCompatibility()
        {
            using (var stream = new MemoryStream())
            using (var zlib = new ZlibStream(stream, Ionic.Zlib.CompressionMode.Compress, CompressionLevel.BestSpeed))
            {
                byte[] payload = { 1, 2, 3, 4 };
                zlib.Write(payload, 0, payload.Length);
                Require(zlib.TotalIn == payload.Length, "Zlib TotalIn diagnostic changed");
                Require(zlib.TotalOut == -1L, "Zlib TotalOut diagnostic changed");
            }
        }

        private static void VerifyZoneCopiedAssets(string repositoryRoot, string zoneOutput)
        {
            VerifyCopiedAssetsFromInventory(
                repositoryRoot,
                zoneOutput,
                "LinuxBuild/source-inventory/ZoneEngine.ContentItems.props");
            VerifyCopiedAssetsFromInventory(
                repositoryRoot,
                zoneOutput,
                "LinuxBuild/source-inventory/ZoneEngine.RuntimeCopyItems.props");
        }

        private static void VerifyCopiedAssetsFromInventory(
            string repositoryRoot,
            string zoneOutput,
            string inventoryPath)
        {
            string fullInventoryPath = Path.Combine(repositoryRoot, inventoryPath.Replace('/', Path.DirectorySeparatorChar));
            XDocument document = XDocument.Parse(File.ReadAllText(fullInventoryPath));
            foreach (XElement content in document.Descendants("Content"))
            {
                if (content.Attribute("CopyToOutputDirectory") == null)
                {
                    continue;
                }

                string include = content.Attribute("Include").Value.Replace(
                    "$(AORebirthRepositoryRoot)",
                    repositoryRoot);
                string link = content.Attribute("Link").Value.Replace('/', Path.DirectorySeparatorChar);
                string outputPath = Path.Combine(zoneOutput, link);
                Require(File.Exists(include), "Missing source asset: " + include);
                Require(File.Exists(outputPath), "Missing copied Zone asset: " + link);
                Require(
                    HashFile(include).SequenceEqual(HashFile(outputPath)),
                    "Copied Zone asset hash changed: " + link);
            }
        }

        private static byte[] HashFile(string path)
        {
            using (var sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return sha256.ComputeHash(stream);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class CaseDto
        {
            public int Value = -1;

            public string Name { get; set; }
        }
    }
}
