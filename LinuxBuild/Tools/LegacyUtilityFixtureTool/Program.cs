using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Utility;

namespace AORebirth.LinuxBuild.LegacyUtilityFixtureTool
{
    internal static class Program
    {
        private const string Version = "stage1-legacy";

        private static readonly int[] Values =
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
                if (args.Length != 2 || (args[0] != "write" && args[0] != "verify"))
                {
                    throw new ArgumentException("Usage: LegacyUtilityFixtureTool (write|verify) <directory>");
                }

                string directory = Path.GetFullPath(args[1]);
                string listPath = Path.Combine(directory, "LegacyUtilityList.dat");
                string dictionaryPath = Path.Combine(directory, "LegacyUtilityDictionary.dat");
                if (args[0] == "write")
                {
                    Directory.CreateDirectory(directory);
                    MessagePackZip.CompressData(listPath, Version, Values.ToList(), 3);
                    MessagePackZip.CompressData(dictionaryPath, Version, CreateDictionary(), 3);
                    WriteManifest(directory, listPath, dictionaryPath);
                    Console.WriteLine("WROTE: " + directory);
                    return 0;
                }

                Verify(listPath);
                Console.WriteLine("PASS: legacy Utility list fixture verification");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception.Message);
                return 1;
            }
        }

        private static Dictionary<string, int> CreateDictionary()
        {
            var result = new Dictionary<string, int>();
            for (int index = 0; index < Values.Length; index++)
            {
                result.Add("value-" + index, Values[index]);
            }

            return result;
        }

        private static void Verify(string listPath)
        {
            List<int> list = MessagePackZip.UncompressData<int>(listPath);
            if (!list.SequenceEqual(Values))
            {
                throw new InvalidDataException("List fixture values changed.");
            }
        }

        private static void WriteManifest(string directory, params string[] fixturePaths)
        {
            string manifestPath = Path.Combine(directory, "LegacyUtilityFixtures.manifest");
            string[] lines = fixturePaths.Select(CreateManifestLine).ToArray();
            File.WriteAllLines(manifestPath, lines, new UTF8Encoding(false));
        }

        private static string CreateManifestLine(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string hash = string.Concat(
                    sha256.ComputeHash(stream)
                        .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}|{1}|{2}",
                    Path.GetFileName(path),
                    new FileInfo(path).Length,
                    hash);
            }
        }
    }
}
