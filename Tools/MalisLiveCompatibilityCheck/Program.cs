using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Web.Script.Serialization;

namespace AORebirth.MalisLiveCompatibilityCheck
{
    internal static class Program
    {
        private static string _runtimeDirectory;
        private static readonly List<string> PluginDirectories = new List<string>();

        private static int Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.Error.WriteLine("Usage: MalisLiveCompatibilityCheck <runtime-dir> <malis-dll> <harvester-dll> <metadata-json>");
                return 2;
            }

            _runtimeDirectory = Path.GetFullPath(args[0]);
            string malisPath = Path.GetFullPath(args[1]);
            string harvesterPath = Path.GetFullPath(args[2]);
            string metadataPath = Path.GetFullPath(args[3]);
            PluginDirectories.Add(Path.GetDirectoryName(malisPath));
            PluginDirectories.Add(Path.GetDirectoryName(harvesterPath));
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveReflectionOnlyAssembly;

            try
            {
                Dictionary<string, object> malis = InspectPlugin(malisPath, "MaliMissionRoller2.Main");
                Dictionary<string, object> harvester = InspectPlugin(harvesterPath, "AORebirth.MissionEvidence.MissionOfferHarvester");
                List<Dictionary<string, object>> runtime = new List<Dictionary<string, object>>
                {
                    InspectAssembly(Path.Combine(_runtimeDirectory, "AOSharp.Bootstrap.dll")),
                    InspectAssembly(Path.Combine(_runtimeDirectory, "AOSharp.Common.dll")),
                    InspectAssembly(Path.Combine(_runtimeDirectory, "AOSharp.Core.dll")),
                    InspectAssembly(Path.Combine(_runtimeDirectory, "AOSharp.exe")),
                    InspectAssembly(Path.Combine(_runtimeDirectory, "Newtonsoft.Json.dll"))
                };

                string malisCore = GetReferenceIdentity(malis, "AOSharp.Core");
                string harvesterCore = GetReferenceIdentity(harvester, "AOSharp.Core");
                string runtimeCore = (string)runtime.Single(row => (string)row["Name"] == "AOSharp.Core")["FullName"];
                Require(malisCore == runtimeCore, "Malis AOSharp.Core reference does not match installed runtime.");
                Require(harvesterCore == runtimeCore, "Harvester AOSharp.Core reference does not match installed runtime.");
                Require(malisCore == harvesterCore, "Plugins do not share one AOSharp.Core identity.");

                Dictionary<string, object> output = new Dictionary<string, object>
                {
                    ["SchemaVersion"] = 1,
                    ["RuntimeDirectory"] = _runtimeDirectory,
                    ["RuntimeAssemblies"] = runtime,
                    ["Plugins"] = new[] { malis, harvester },
                    ["SharedAOSharpCoreIdentity"] = runtimeCore,
                    ["PluginEntryValidation"] = "PASS",
                    ["DependencyClosure"] = "PASS",
                    ["CoexistenceAssemblyIdentity"] = "PASS",
                    ["LiveGameplayValidation"] = "NOT_PERFORMED"
                };
                Directory.CreateDirectory(Path.GetDirectoryName(metadataPath));
                File.WriteAllText(metadataPath, new JavaScriptSerializer().Serialize(output) + Environment.NewLine);
                Console.WriteLine("MALIS_PLUGIN_ENTRY=PASS");
                Console.WriteLine("HARVESTER_PLUGIN_ENTRY=PASS");
                Console.WriteLine("MALIS_DEPENDENCY_CLOSURE=PASS");
                Console.WriteLine("MALIS_HARVESTER_COEXISTENCE_ASSEMBLY_IDENTITY=PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("MALIS_LIVE_COMPATIBILITY=FAIL: " + error.Message);
                return 1;
            }
        }

        private static Dictionary<string, object> InspectPlugin(string path, string expectedEntryType)
        {
            Assembly assembly = Assembly.ReflectionOnlyLoadFrom(path);
            List<Dictionary<string, object>> references = ResolveReferences(assembly);
            Type entry = assembly.GetExportedTypes().SingleOrDefault(type =>
                type.FullName == expectedEntryType && type.GetInterfaces().Any(iface => iface.FullName == "AOSharp.Core.IAOPluginEntry"));
            Require(entry != null, "Expected AOSharp entry type is missing: " + expectedEntryType);
            Require(entry.GetConstructor(Type.EmptyTypes) != null, "Entry type lacks a public parameterless constructor: " + expectedEntryType);
            MethodInfo init = entry.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            MethodInfo teardown = entry.GetMethod("Teardown", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Require(init != null, "Entry type lacks public Init(string): " + expectedEntryType);
            Require(teardown != null, "Entry type lacks public Teardown(): " + expectedEntryType);
            return new Dictionary<string, object>
            {
                ["Path"] = path,
                ["Name"] = assembly.GetName().Name,
                ["FullName"] = assembly.FullName,
                ["TargetFramework"] = GetTargetFramework(assembly),
                ["EntryType"] = expectedEntryType,
                ["PublicParameterlessConstructor"] = true,
                ["PublicInitString"] = true,
                ["PublicTeardown"] = true,
                ["References"] = references
            };
        }

        private static Dictionary<string, object> InspectAssembly(string path)
        {
            Require(File.Exists(path), "Installed runtime assembly is missing: " + path);
            AssemblyName name = AssemblyName.GetAssemblyName(path);
            return new Dictionary<string, object>
            {
                ["Path"] = path,
                ["Name"] = name.Name,
                ["FullName"] = name.FullName,
                ["Version"] = name.Version.ToString(),
                ["ProcessorArchitecture"] = name.ProcessorArchitecture.ToString()
            };
        }

        private static List<Dictionary<string, object>> ResolveReferences(Assembly assembly)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                Assembly resolved = ResolveByName(reference);
                Require(resolved != null, "Unresolved reference " + reference.FullName + " for " + assembly.FullName);
                result.Add(new Dictionary<string, object>
                {
                    ["Name"] = reference.Name,
                    ["RequestedIdentity"] = reference.FullName,
                    ["ResolvedIdentity"] = resolved.FullName,
                    ["ResolvedLocation"] = resolved.Location
                });
            }
            return result;
        }

        private static Assembly ResolveByName(AssemblyName name)
        {
            foreach (string directory in new[] { _runtimeDirectory }.Concat(PluginDirectories))
            {
                string candidate = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(candidate))
                {
                    AssemblyName candidateName = AssemblyName.GetAssemblyName(candidate);
                    if (candidateName.FullName == name.FullName)
                        return Assembly.ReflectionOnlyLoadFrom(candidate);
                }
            }
            try
            {
                return Assembly.ReflectionOnlyLoad(name.FullName);
            }
            catch
            {
                return null;
            }
        }

        private static Assembly ResolveReflectionOnlyAssembly(object sender, ResolveEventArgs args)
        {
            return ResolveByName(new AssemblyName(args.Name));
        }

        private static string GetReferenceIdentity(Dictionary<string, object> plugin, string name)
        {
            List<Dictionary<string, object>> references = (List<Dictionary<string, object>>)plugin["References"];
            return (string)references.Single(reference => (string)reference["Name"] == name)["RequestedIdentity"];
        }

        private static string GetTargetFramework(Assembly assembly)
        {
            CustomAttributeData attribute = assembly.GetCustomAttributesData().SingleOrDefault(item => item.Constructor.DeclaringType == typeof(TargetFrameworkAttribute));
            return attribute == null ? null : (string)attribute.ConstructorArguments[0].Value;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
