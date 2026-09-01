namespace AOSharpCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Script.Serialization;

    using AORebirth.Core.Events;
    using AORebirth.Core.Items;

    using MsgPack;
    using MsgPack.Serialization;

    using Utility;

    internal static class ItemTemplateProjection
    {
        public static int RunAll(
            string itemDatabasePath,
            string expectedSha256,
            string expectedByteLengthText,
            string outputPath)
        {
            try
            {
                long expectedByteLength;
                if (!long.TryParse(
                    expectedByteLengthText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out expectedByteLength)
                    || expectedByteLength < 0)
                {
                    throw new InvalidDataException("Item database byte length is invalid.");
                }

                VerifyItemDatabase(itemDatabasePath, expectedSha256, expectedByteLength);
                ItemTemplate[] templates = MessagePackZip
                    .UncompressData<ItemTemplate>(itemDatabasePath)
                    .OrderBy(value => value.ID)
                    .ToArray();
                if (templates.Length == 0)
                {
                    throw new InvalidDataException("Item database contains no templates.");
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue,
                    RecursionLimit = 256,
                };
                using (StreamWriter writer = new StreamWriter(
                    outputPath,
                    false,
                    new UTF8Encoding(false)))
                {
                    foreach (ItemTemplate template in templates)
                    {
                        writer.WriteLine(
                            serializer.Serialize(
                                new SortedDictionary<string, object>(StringComparer.Ordinal)
                                {
                                    { "flags", template.Flags },
                                    { "item_id", template.ID },
                                    { "item_type", template.ItemType },
                                    { "quality_level", template.Quality },
                                    {
                                        "relations",
                                        (template.Relations ?? new List<int>())
                                            .OrderBy(value => value)
                                            .ToArray()
                                    },
                                }));
                    }
                }

                Console.WriteLine(
                    "AORebirth item template export PASS templates="
                    + templates.Length.ToString(CultureInfo.InvariantCulture));
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Item template export failed: " + error.Message);
                return 1;
            }
        }

        public static int Run(
            string itemDatabasePath,
            string expectedSha256,
            string expectedByteLengthText,
            string templateIdsText,
            string outputPath)
        {
            try
            {
                long expectedByteLength;
                if (!long.TryParse(
                    expectedByteLengthText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out expectedByteLength)
                    || expectedByteLength < 0)
                {
                    throw new InvalidDataException("Item database byte length is invalid.");
                }

                HashSet<int> requestedIds = ParseTemplateIds(templateIdsText);
                VerifyItemDatabase(itemDatabasePath, expectedSha256, expectedByteLength);

                SortedDictionary<string, object> templates =
                    new SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (ItemTemplate template in
                    MessagePackZip.UncompressData<ItemTemplate>(itemDatabasePath))
                {
                    int templateId = template.ID;
                    if (!requestedIds.Contains(templateId))
                    {
                        continue;
                    }

                    templates.Add(
                        templateId.ToString(CultureInfo.InvariantCulture),
                        new Dictionary<string, object>
                        {
                            { "actions", ConvertValue(PackToObject(template.Events)) },
                            {
                                "qualityLevel",
                                template.Quality
                            },
                            {
                                "stats",
                                template.Stats.ToDictionary(
                                    row => row.Key.ToString(CultureInfo.InvariantCulture),
                                    row => (object)row.Value)
                            },
                        });
                }

                int[] missingIds = requestedIds
                    .Where(id => !templates.ContainsKey(id.ToString(CultureInfo.InvariantCulture)))
                    .OrderBy(id => id)
                    .ToArray();
                if (missingIds.Length != 0)
                {
                    throw new InvalidDataException(
                        "Item database is missing requested templates: "
                        + string.Join(",", missingIds));
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue,
                    RecursionLimit = 256,
                };
                string rendered = serializer.Serialize(
                    new Dictionary<string, object>
                    {
                        { "templates", templates },
                    });
                File.WriteAllText(outputPath, rendered + "\n", new UTF8Encoding(false));
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Item template projection failed: " + error.Message);
                return 1;
            }
        }

        private static HashSet<int> ParseTemplateIds(string value)
        {
            HashSet<int> result = new HashSet<int>();
            foreach (string part in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int templateId;
                if (!int.TryParse(
                    part,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out templateId)
                    || templateId <= 0)
                {
                    throw new InvalidDataException("Item template ID list is invalid.");
                }

                result.Add(templateId);
            }

            if (result.Count == 0)
            {
                throw new InvalidDataException("Item template ID list is empty.");
            }

            return result;
        }

        private static void VerifyItemDatabase(
            string path,
            string expectedSha256,
            long expectedByteLength)
        {
            FileInfo source = new FileInfo(path);
            if (!source.Exists || source.Length != expectedByteLength)
            {
                throw new InvalidDataException("Item database byte length does not match its descriptor.");
            }

            string actualSha256;
            using (SHA256 hasher = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                actualSha256 = string.Concat(
                    hasher.ComputeHash(stream).Select(value => value.ToString("x2")));
            }

            if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Item database SHA-256 does not match its descriptor.");
            }
        }

        private static object ConvertValue(MessagePackObject value)
        {
            if (value.IsNil)
            {
                return null;
            }

            if (value.IsList)
            {
                return value.AsList().Select(ConvertValue).ToList();
            }

            if (value.IsDictionary)
            {
                SortedDictionary<string, object> result =
                    new SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<MessagePackObject, MessagePackObject> entry
                    in value.AsDictionary())
                {
                    result.Add(
                        Convert.ToString(entry.Key.ToObject(), CultureInfo.InvariantCulture),
                        ConvertValue(entry.Value));
                }

                return result;
            }

            object scalar = value.ToObject();
            byte[] binary = scalar as byte[];
            return binary == null ? scalar : Convert.ToBase64String(binary);
        }

        private static MessagePackObject PackToObject(List<Event> events)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                MessagePackSerializer<List<Event>> serializer =
                    MessagePackSerializer.Create<List<Event>>();
                serializer.Pack(stream, events);
                stream.Position = 0;
                return Unpacking.UnpackObject(stream);
            }
        }
    }
}
