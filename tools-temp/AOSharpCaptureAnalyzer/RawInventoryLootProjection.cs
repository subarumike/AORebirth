namespace AOSharpCaptureAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    internal static class RawInventoryLootProjection
    {
        private const int InventoryUpdateType = 0x4E536976;
        private const int ContainerAddItemType = 0x47537A24;

        internal static int Run(string captureFolder)
        {
            if (string.IsNullOrWhiteSpace(captureFolder) || !Directory.Exists(captureFolder))
            {
                Console.Error.WriteLine("Capture folder does not exist: " + captureFolder);
                return 1;
            }

            string rawPath = Path.Combine(captureFolder, "raw-packets.csv");
            if (!File.Exists(rawPath))
            {
                Console.Error.WriteLine("raw-packets.csv does not exist: " + rawPath);
                return 1;
            }

            string inventoryPath = Path.Combine(captureFolder, "inventory-updates.csv");
            string containerPath = Path.Combine(captureFolder, "container-add-items.csv");
            string lootPath = Path.Combine(captureFolder, "corpse-loot-observations.csv");
            string errorPath = Path.Combine(captureFolder, "inventory-decode-errors.csv");
            string pendingInventoryPath = inventoryPath + ".pending";
            string pendingContainerPath = containerPath + ".pending";
            string pendingLootPath = lootPath + ".pending";
            string pendingErrorPath = errorPath + ".pending";

            DeleteIfExists(pendingInventoryPath);
            DeleteIfExists(pendingContainerPath);
            DeleteIfExists(pendingLootPath);
            DeleteIfExists(pendingErrorPath);

            int inventoryMessages = 0;
            int inventoryRows = 0;
            int containerMessages = 0;
            int corpseSnapshots = 0;
            int errors = 0;
            var corpseOpenCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            SerializerResolver resolver = new SerializerResolverBuilder<MessageBody>().Build();

            using (var inventory = CreateWriter(pendingInventoryPath))
            using (var containers = CreateWriter(pendingContainerPath))
            using (var loot = CreateWriter(pendingLootPath))
            using (var error = CreateWriter(pendingErrorPath))
            {
                inventory.WriteLine("CapturedUtc,Direction,Sequence,InventoryIdentity,Handle,Slot,Placement,Flags,Count,ItemIdentity,LowId,HighId,Quality,Unknown");
                containers.WriteLine("CapturedUtc,Direction,Sequence,MessageIdentity,SourceContainer,Target,TargetPlacement");
                loot.WriteLine("CapturedUtc,Direction,Sequence,CorpseIdentity,OpenOrdinal,InitialSnapshot,ItemCount,DeadNpcIdentity,EnemyName,MonsterData,EnemyLevel,CorpseCredits,PlayerIdentity,PlayerLevel,PlayfieldId,Items,CorrelationStatus");
                error.WriteLine("CapturedUtc,Direction,Sequence,MessageType,DecodeError,RawHex");

                foreach (RawPacketRow row in ReadRawPacketRows(rawPath))
                {
                    if (row.N3Type != InventoryUpdateType && row.N3Type != ContainerAddItemType)
                    {
                        continue;
                    }

                    try
                    {
                        byte[] packet = FromHex(row.RawHex);
                        if (packet.Length < 16)
                        {
                            throw new InvalidDataException("Packet is shorter than the 16-byte N3 envelope.");
                        }

                        byte[] body = new byte[packet.Length - 16];
                        Buffer.BlockCopy(packet, 16, body, 0, body.Length);
                        if (row.N3Type == InventoryUpdateType)
                        {
                            InventoryUpdateMessage message = Deserialize<InventoryUpdateMessage>(resolver, body);
                            InventoryEntry[] entries = message.Entries ?? new InventoryEntry[0];
                            string inventoryIdentity = IdentityText(message.BagIdentity);
                            inventoryMessages++;
                            for (int index = 0; index < entries.Length; index++)
                            {
                                InventoryEntry entry = entries[index];
                                inventory.WriteLine(
                                    string.Join(
                                        ",",
                                        Csv(row.CapturedUtc),
                                        Csv(row.Direction),
                                        row.Sequence.ToString(CultureInfo.InvariantCulture),
                                        Csv(inventoryIdentity),
                                        Csv(IdentityText(message.Identity)),
                                        index.ToString(CultureInfo.InvariantCulture),
                                        entry.Slotnumber.ToString(CultureInfo.InvariantCulture),
                                        entry.UnknownFlags.ToString(CultureInfo.InvariantCulture),
                                        entry.Unknown1.ToString(CultureInfo.InvariantCulture),
                                        Csv(IdentityText(entry.Identity)),
                                        entry.LowId.ToString(CultureInfo.InvariantCulture),
                                        entry.HighId.ToString(CultureInfo.InvariantCulture),
                                        entry.Quality.ToString(CultureInfo.InvariantCulture),
                                        entry.Unknown2.ToString(CultureInfo.InvariantCulture)));
                                inventoryRows++;
                            }

                            if (inventoryIdentity.IndexOf("Corpse", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                int openOrdinal;
                                corpseOpenCounts.TryGetValue(inventoryIdentity, out openOrdinal);
                                openOrdinal++;
                                corpseOpenCounts[inventoryIdentity] = openOrdinal;
                                string itemSummary = string.Join(
                                    ";",
                                    entries.Select(
                                        entry => string.Format(
                                            CultureInfo.InvariantCulture,
                                            "{0}:{1}:{2}:{3}",
                                            entry.LowId,
                                            entry.HighId,
                                            entry.Quality,
                                            entry.Unknown1)).ToArray());
                                loot.WriteLine(
                                    string.Join(
                                        ",",
                                        Csv(row.CapturedUtc),
                                        Csv(row.Direction),
                                        row.Sequence.ToString(CultureInfo.InvariantCulture),
                                        Csv(inventoryIdentity),
                                        openOrdinal.ToString(CultureInfo.InvariantCulture),
                                        openOrdinal == 1 ? "true" : "false",
                                        entries.Length.ToString(CultureInfo.InvariantCulture),
                                        Csv(string.Empty),
                                        Csv(string.Empty),
                                        Csv(string.Empty),
                                        Csv(string.Empty),
                                        Csv(string.Empty),
                                        Csv(string.Empty),
                                        Csv(string.Empty),
                                        Csv(string.Empty),
                                        Csv(itemSummary),
                                        Csv("unlinked-offline-generation")));
                                corpseSnapshots++;
                            }
                        }
                        else
                        {
                            ContainerAddItemMessage message = Deserialize<ContainerAddItemMessage>(resolver, body);
                            containers.WriteLine(
                                string.Join(
                                    ",",
                                    Csv(row.CapturedUtc),
                                    Csv(row.Direction),
                                    row.Sequence.ToString(CultureInfo.InvariantCulture),
                                    Csv(IdentityText(message.Identity)),
                                    Csv(IdentityText(message.SourceContainer)),
                                    Csv(IdentityText(message.Target)),
                                    message.TargetPlacement.ToString(CultureInfo.InvariantCulture)));
                            containerMessages++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        error.WriteLine(
                            string.Join(
                                ",",
                                Csv(row.CapturedUtc),
                                Csv(row.Direction),
                                row.Sequence.ToString(CultureInfo.InvariantCulture),
                                row.N3Type.ToString(CultureInfo.InvariantCulture),
                                Csv(ex.GetType().Name + ": " + ex.Message),
                                Csv(row.RawHex)));
                    }
                }
            }

            if (errors == 0)
            {
                Promote(pendingInventoryPath, inventoryPath);
                Promote(pendingContainerPath, containerPath);
                Promote(pendingLootPath, lootPath);
                Promote(pendingErrorPath, errorPath);
            }

            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "inventoryMessages={0} inventoryRows={1} containerAddMessages={2} corpseSnapshots={3} decodeErrors={4}",
                    inventoryMessages,
                    inventoryRows,
                    containerMessages,
                    corpseSnapshots,
                    errors));
            return errors == 0 ? 0 : 1;
        }

        internal static void RunSelfTest()
        {
            string actual = IdentityText(
                new Identity { Type = IdentityType.Corpse, Instance = 868353 });
            const string expected = "Corpse:000D4001";
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Loot identity formatting mismatch. Expected "
                    + expected
                    + " but found "
                    + actual
                    + ".");
            }
        }

        private static T Deserialize<T>(SerializerResolver resolver, byte[] body)
            where T : MessageBody
        {
            ISerializer serializer = resolver.GetSerializer(typeof(T));
            using (var memory = new MemoryStream(body))
            using (var reader = new SmokeLounge.AOtomation.Messaging.Serialization.StreamReader(memory))
            {
                return (T)serializer.Deserialize(reader, new SerializationContext(resolver));
            }
        }

        private static IEnumerable<RawPacketRow> ReadRawPacketRows(string path)
        {
            using (var reader = new System.IO.StreamReader(path, Encoding.UTF8, true))
            {
                string headerLine = reader.ReadLine();
                if (headerLine == null)
                {
                    yield break;
                }

                List<string> headers = ParseCsv(headerLine);
                var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < headers.Count; index++)
                {
                    indexes[headers[index].TrimStart('\uFEFF')] = index;
                }

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    List<string> values = ParseCsv(line);
                    int n3Type;
                    int sequence;
                    if (!int.TryParse(Value(values, indexes, "N3TypeValue"), NumberStyles.Integer, CultureInfo.InvariantCulture, out n3Type)
                        || !int.TryParse(Value(values, indexes, "Sequence"), NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence))
                    {
                        continue;
                    }

                    yield return new RawPacketRow
                    {
                        CapturedUtc = Value(values, indexes, "CapturedUtc"),
                        Direction = Value(values, indexes, "Direction"),
                        Sequence = sequence,
                        N3Type = n3Type,
                        RawHex = Value(values, indexes, "RawHex")
                    };
                }
            }
        }

        private static List<string> ParseCsv(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (quoted)
                {
                    if (character == '"' && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else if (character == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        current.Append(character);
                    }
                }
                else if (character == '"')
                {
                    quoted = true;
                }
                else if (character == ',')
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(character);
                }
            }

            values.Add(current.ToString());
            return values;
        }

        private static string Value(List<string> values, Dictionary<string, int> indexes, string name)
        {
            int index;
            return indexes.TryGetValue(name, out index) && index >= 0 && index < values.Count
                       ? values[index]
                       : string.Empty;
        }

        private static string IdentityText(Identity identity)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:X8}",
                identity.Type,
                unchecked((uint)identity.Instance));
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static byte[] FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Length % 2 != 0)
            {
                throw new InvalidDataException("Raw packet hex is empty or has an odd length.");
            }

            byte[] result = new byte[hex.Length / 2];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = byte.Parse(hex.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static System.IO.StreamWriter CreateWriter(string path)
        {
            return new System.IO.StreamWriter(path, false, new UTF8Encoding(false));
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void Promote(string pendingPath, string finalPath)
        {
            if (File.Exists(finalPath))
            {
                File.Replace(pendingPath, finalPath, null, true);
            }
            else
            {
                File.Move(pendingPath, finalPath);
            }
        }

        private sealed class RawPacketRow
        {
            internal string CapturedUtc { get; set; }
            internal string Direction { get; set; }
            internal int Sequence { get; set; }
            internal int N3Type { get; set; }
            internal string RawHex { get; set; }
        }
    }
}
